using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class AccountSecurityTests
{
    private static readonly Guid Admin = WorkflowApiFactory.AccountId("admin@ovo.test");
    private static async Task Db(WorkflowApiFactory f, Func<AppDbContext, Task> action)
    { await using var scope = f.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
    private static async Task<JsonElement> Login(HttpClient c, string role = "admin")
    { var r = await c.PostAsJsonAsync("/api/auth/login", new { email = role+"@ovo.test", password = WorkflowApiFactory.TestPassword }); r.EnsureSuccessStatusCode(); return await r.Content.ReadFromJsonAsync<JsonElement>(); }
    private static void Session(HttpClient c, JsonElement response) => c.DefaultRequestHeaders.Authorization = new("Bearer", response.GetProperty("token").GetString());
    private static string Code(string secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
    private static async Task<(string Secret, string[] Codes)> Enable(HttpClient c)
    {
        Session(c, await Login(c));
        var setup = await c.PostAsJsonAsync("/api/auth/security/setup", new SecurityProof(WorkflowApiFactory.TestPassword, "")); setup.EnsureSuccessStatusCode();
        var data = await setup.Content.ReadFromJsonAsync<JsonElement>(); var secret = data.GetProperty("secret").GetString()!;
        var codes = data.GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var enable = await c.PostAsJsonAsync("/api/auth/security/enable", new SecurityProof(WorkflowApiFactory.TestPassword, Code(secret), true)); enable.EnsureSuccessStatusCode();
        Session(c, await enable.Content.ReadFromJsonAsync<JsonElement>()); return (secret, codes);
    }

    [Fact]
    public async Task Enrollment_requires_password_saved_recovery_and_current_code_and_revokes_old_sessions()
    {
        await using var f = new WorkflowApiFactory(); using var c = f.CreateClient(); var login = await Login(c); Session(c, login);
        using var old = f.CreateClient(); Session(old, login);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/security/setup", new SecurityProof("wrong", ""))).StatusCode);
        var setup = await c.PostAsJsonAsync("/api/auth/security/setup", new SecurityProof(WorkflowApiFactory.TestPassword, "")); setup.EnsureSuccessStatusCode();
        var data = await setup.Content.ReadFromJsonAsync<JsonElement>(); var secret = data.GetProperty("secret").GetString()!;
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/security/enable", new SecurityProof(WorkflowApiFactory.TestPassword, Code(secret)))).StatusCode);
        (await c.PostAsJsonAsync("/api/auth/security/enable", new SecurityProof(WorkflowApiFactory.TestPassword, Code(secret), true))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await old.GetAsync("/api/auth/me")).StatusCode);
        var challenge = await Login(c); Assert.True(challenge.GetProperty("requiresSecondFactor").GetBoolean()); Assert.False(challenge.TryGetProperty("token", out _));
        var recovery = data.GetProperty("recoveryCodes")[0].GetString()!;
        var verified = await c.PostAsJsonAsync("/api/auth/second-factor", new SecondFactorRequest(challenge.GetProperty("challenge").GetString()!, recovery)); verified.EnsureSuccessStatusCode(); Session(c, await verified.Content.ReadFromJsonAsync<JsonElement>());
        var status = await c.GetStringAsync("/api/auth/security"); Assert.Contains("\"recoveryCodesRemaining\":9", status); Assert.DoesNotContain(secret, status); Assert.DoesNotContain(recovery, status);
        await Db(f, async db => { var state = await db.AccountSecurities.SingleAsync(); Assert.DoesNotContain(secret, state.ProtectedSecret); Assert.DoesNotContain(recovery, string.Join(',', state.RecoveryHashes)); Assert.DoesNotContain(secret, string.Join(',', await db.AuditRecords.Select(x => x.NewValueJson).ToListAsync())); });
    }

    [Fact]
    public async Task Recovery_code_and_challenge_are_single_use_even_for_the_only_admin()
    {
        await using var f = new WorkflowApiFactory(); using var c = f.CreateClient(); var (_, codes) = await Enable(c);
        var challenge = (await Login(c)).GetProperty("challenge").GetString()!;
        var r = new SecondFactorRequest(challenge, codes[0]); (await c.PostAsJsonAsync("/api/auth/second-factor", r)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/second-factor", r)).StatusCode);
        var next = (await Login(c)).GetProperty("challenge").GetString()!;
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/second-factor", new SecondFactorRequest(next, codes[0]))).StatusCode);
        (await c.PostAsJsonAsync("/api/auth/second-factor", new SecondFactorRequest(next, codes[1]))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Totp_replay_is_rejected_and_next_window_can_authenticate()
    {
        await using var f = new WorkflowApiFactory(); using var c = f.CreateClient(); var (secret, _) = await Enable(c);
        long usedStep = 0; await Db(f, async db => usedStep = (await db.AccountSecurities.SingleAsync()).LastTimeStep);
        var challenge = (await Login(c)).GetProperty("challenge").GetString()!;
        var replay = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTimeOffset.FromUnixTimeSeconds(usedStep * 30).UtcDateTime);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/second-factor", new SecondFactorRequest(challenge, replay))).StatusCode);
        var nextCode = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow.AddSeconds(30));
        (await c.PostAsJsonAsync("/api/auth/second-factor", new SecondFactorRequest(challenge, nextCode))).EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("closed")]
    [InlineData("password-reset")]
    public async Task Challenge_rejects_expiry_or_account_changes_without_disabling_second_factor(string change)
    {
        await using var f = new WorkflowApiFactory(); using var c = f.CreateClient(); var (_, codes) = await Enable(c);
        var challenge = (await Login(c)).GetProperty("challenge").GetString()!;
        await Db(f, async db => { if (change == "expired") (await db.AccountSecurities.SingleAsync()).ChallengeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); else { var user = await db.UserAccounts.SingleAsync(x => x.Id == Admin); if (change == "closed") user.IsActive = false; else user.TokenVersion++; } await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/second-factor", new SecondFactorRequest(challenge, codes[0]))).StatusCode);
        await Db(f, async db => Assert.True((await db.AccountSecurities.SingleAsync()).Enabled));
    }

    [Fact]
    public async Task Five_failures_lock_the_account_across_new_challenges()
    {
        await using var f = new WorkflowApiFactory(); using var c = f.CreateClient(); var (_, codes) = await Enable(c);
        var challenge = (await Login(c)).GetProperty("challenge").GetString()!;
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/second-factor", new SecondFactorRequest(challenge, "invalid"))).StatusCode);
        await Db(f, async db => { var s = await db.AccountSecurities.SingleAsync(); Assert.Equal(5, s.FailedAttempts); Assert.True(s.LockedUntil > DateTimeOffset.UtcNow); });
        // IP limit may also apply. Verify the account lock directly, independent of IP rate limiting.
        await using var scope = f.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AccountSecurityService>().CompleteLogin(new SecondFactorRequest(challenge, codes[0]));
        Assert.Equal(400, ((Microsoft.AspNetCore.Http.IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public async Task Recovery_regeneration_and_disable_require_both_factors()
    {
        await using var f = new WorkflowApiFactory(); using var c = f.CreateClient(); var (_, codes) = await Enable(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/security/disable", new SecurityProof(WorkflowApiFactory.TestPassword, "bad"))).StatusCode);
        var renewed = await c.PostAsJsonAsync("/api/auth/security/recovery-codes", new SecurityProof(WorkflowApiFactory.TestPassword, codes[0])); renewed.EnsureSuccessStatusCode();
        var data = await renewed.Content.ReadFromJsonAsync<JsonElement>(); Session(c, data);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/security/disable", new SecurityProof(WorkflowApiFactory.TestPassword, codes[1]))).StatusCode);
        var disable = await c.PostAsJsonAsync("/api/auth/security/disable", new SecurityProof(WorkflowApiFactory.TestPassword, data.GetProperty("recoveryCodes")[0].GetString()!)); disable.EnsureSuccessStatusCode();
        var login = await Login(c); Assert.True(login.TryGetProperty("token", out _));
        await Db(f, async db => { var s = await db.AccountSecurities.SingleAsync(); Assert.False(s.Enabled); Assert.Empty(s.ProtectedSecret); Assert.Empty(s.RecoveryHashes); });
    }

    [Fact]
    public async Task Non_admin_cannot_enroll_and_old_pending_setup_expires()
    {
        await using var f = new WorkflowApiFactory(); using var c = f.CreateClient(); Session(c, await Login(c, "partner"));
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/auth/security/setup", new SecurityProof(WorkflowApiFactory.TestPassword, ""))).StatusCode);
        Session(c, await Login(c)); var setup = await c.PostAsJsonAsync("/api/auth/security/setup", new SecurityProof(WorkflowApiFactory.TestPassword, "")); setup.EnsureSuccessStatusCode();
        var secret = (await setup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;
        await Db(f, async db => { (await db.AccountSecurities.SingleAsync(x => x.UserId == Admin)).SetupExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/security/enable", new SecurityProof(WorkflowApiFactory.TestPassword, Code(secret), true))).StatusCode);
    }

    [Fact]
    public async Task Password_reset_endpoint_cannot_bypass_second_factor()
    {
        await using var f = new WorkflowApiFactory(); using var c = f.CreateClient(); await Enable(c);
        var token = new string('A', 64);
        await Db(f, async db => { var user = await db.UserAccounts.SingleAsync(x => x.Id == Admin); db.Add(new AccountLink { UserId = user.Id, Email = user.Email, AccountVersion = user.TokenVersion, Purpose = AccountLinkPurpose.PasswordReset, TokenHash = AccountSecurityService.Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30) }); await db.SaveChangesAsync(); });
        const string newPassword = "Reset-test-password-2026!";
        (await c.PostAsJsonAsync("/api/auth/complete-account", new { token, password = newPassword })).EnsureSuccessStatusCode();
        var login = await c.PostAsJsonAsync("/api/auth/login", new { email = "admin@ovo.test", password = newPassword }); login.EnsureSuccessStatusCode();
        var json = await login.Content.ReadFromJsonAsync<JsonElement>(); Assert.True(json.GetProperty("requiresSecondFactor").GetBoolean()); Assert.False(json.TryGetProperty("token", out _));
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
    }
}
