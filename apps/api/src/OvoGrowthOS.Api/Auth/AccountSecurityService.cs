using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Notifications;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Auth;

public sealed record SecurityProof(string Password, string Code, bool RecoverySaved = false);
public sealed record SecondFactorRequest(string Challenge, string Code);

public sealed class AccountSecurityService(AppDbContext db, IDataProtectionProvider protection, JwtTokenService tokens)
{
    internal const string Purpose = "OVO.AccountSecurity.v1";
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static string[] NewRecoveryCodes() => Enumerable.Range(0, 10).Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(16))).ToArray();
    private static IResult Invalid() => Results.BadRequest(new { error = "Bilgiler doğrulanamadı. Kodun güncel olduğunu kontrol edin; fazla denemede beş dakika bekleyin." });
    private static bool Allowed(UserAccount? user) => user is { IsActive: true, InvitationPending: false } && user.Role is "Admin" or "Partner" or "Analyst" or "BrandClient";
    private static bool Locked(AccountSecurity state) => state.LockedUntil > DateTimeOffset.UtcNow;
    private static void Failed(AccountSecurity state)
    {
        if (state.LockedUntil <= DateTimeOffset.UtcNow) { state.FailedAttempts = 0; state.LockedUntil = null; }
        state.FailedAttempts = Math.Min(5, state.FailedAttempts + 1);
        if (state.FailedAttempts >= 5) state.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(5);
        state.Revision++;
    }
    private bool Proof(AccountSecurity state, string? code, bool allowRecovery)
    {
        if (Locked(state) || string.IsNullOrWhiteSpace(code) || code.Length > 64) return false;
        code = code.Trim().Replace(" ", "").ToUpperInvariant();
        if (allowRecovery && code.Length == 32)
        {
            var hash = Hash(code);
            var match = state.RecoveryHashes.FirstOrDefault(x => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(x), Encoding.ASCII.GetBytes(hash)));
            if (match is null) return false;
            state.RecoveryHashes = state.RecoveryHashes.Where(x => x != match).ToArray();
            return true;
        }
        if (code.Length != 6 || code.Any(x => !char.IsAsciiDigit(x))) return false;
        try
        {
            var secret = protection.CreateProtector(Purpose).Unprotect(state.ProtectedSecret);
            if (!new Totp(Base32Encoding.ToBytes(secret)).VerifyTotp(code, out var step, new VerificationWindow(previous: 1, future: 1)) || step <= state.LastTimeStep) return false;
            state.LastTimeStep = step; return true;
        }
        catch (CryptographicException) { return false; }
    }
    private object Session(UserAccount user, string[]? recoveryCodes = null) => new
    { token = tokens.Create(user), expiresAt = DateTimeOffset.UtcNow.AddHours(8), user = new { user.Id, user.Email, user.Name, user.Role }, recoveryCodes };
    private static void ClearChallenge(AccountSecurity state) { state.ChallengeHash = ""; state.ChallengeExpiresAt = null; }
    private static void Success(AccountSecurity state) { state.FailedAttempts = 0; state.LockedUntil = null; state.Revision++; }
    private void Audit(UserAccount user, string action) => db.AuditRecords.Add(new AuditRecord
    { UserId = user.Email, Action = action, EntityType = "UserAccount", EntityId = user.Id.ToString() });

    public async Task<IResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var candidate = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email);
        if (!JwtTokenService.VerifyPassword(request.Password, candidate?.PasswordHash ?? JwtTokenService.DummyPasswordHash) || !Allowed(candidate))
            return Results.Problem(statusCode: 401, title: "E-posta adresi veya şifre hatalı");
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await NotificationService.LockUser(db, candidate!.Id);
        var user = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.Id == candidate!.Id);
        if (!Allowed(user) || user.PasswordHash != candidate!.PasswordHash || user.TokenVersion != candidate.TokenVersion ||
            user.Role == "BrandClient" && !await db.PortalAccesses.AnyAsync(x => x.UserId == user.Id)) return Results.Unauthorized();
        var state = await db.AccountSecurities.SingleOrDefaultAsync(x => x.UserId == user.Id);
        if (state?.Enabled != true) return Results.Ok(Session(user));
        if (Locked(state)) return Invalid();
        var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        state.ChallengeHash = Hash(challenge); state.ChallengeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        state.ChallengeAccountVersion = user.TokenVersion; state.Revision++;
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
        return Results.Ok(new { requiresSecondFactor = true, challenge, expiresAt = state.ChallengeExpiresAt });
    }

    public async Task<IResult> CompleteLogin(SecondFactorRequest request)
    {
        if (request.Challenge is not { Length: 64 } || request.Code is not { Length: > 0 and <= 64 }) return Invalid();
        var hash = Hash(request.Challenge);
        var id = await db.AccountSecurities.Where(x => x.ChallengeHash == hash).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync();
        if (id is null) return Invalid();
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await NotificationService.LockUser(db, id.Value);
        var user = await db.UserAccounts.SingleAsync(x => x.Id == id);
        var state = await db.AccountSecurities.SingleAsync(x => x.UserId == id);
        if (!Allowed(user) || !state.Enabled || state.ChallengeHash != hash || state.ChallengeExpiresAt <= DateTimeOffset.UtcNow ||
            state.ChallengeAccountVersion != user.TokenVersion || Locked(state) ||
            user.Role == "BrandClient" && !await db.PortalAccesses.AnyAsync(x => x.UserId == user.Id)) return Invalid();
        if (!Proof(state, request.Code, true))
        {
            Failed(state); await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Invalid();
        }
        Success(state); ClearChallenge(state); Audit(user, "SecondFactorLogin");
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.Ok(Session(user));
    }

    public async Task<IResult> Manage(string action, SecurityProof request, ClaimsPrincipal actor)
    {
        var id = Guid.Parse(actor.FindFirstValue("uid")!);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        if (tx is not null) await NotificationService.LockUser(db, id);
        var user = await db.UserAccounts.SingleAsync(x => x.Id == id);
        if (!Allowed(user) || user.TokenVersion.ToString() != actor.FindFirstValue("session_version")) return Results.Unauthorized();
        var state = await db.AccountSecurities.SingleOrDefaultAsync(x => x.UserId == id);
        if (state is null) { state = new AccountSecurity { UserId = id }; db.Add(state); }
        if (Locked(state)) return Invalid();
        if (request.Password is not { Length: > 0 and <= 256 } || !JwtTokenService.VerifyPassword(request.Password, user.PasswordHash))
        { Failed(state); await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Invalid(); }
        if (action == "setup")
        {
            if (user.Role != "Admin") return Results.Forbid();
            if (state.Enabled) return Results.Conflict(new { error = "İki aşamalı giriş zaten açık. Telefon değişiminde önce mevcut kod veya kurtarma koduyla kapatın." });
            var secret = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));
            var codes = NewRecoveryCodes(); state.RecoveryHashes = codes.Select(Hash).ToArray();
            state.ProtectedSecret = protection.CreateProtector(Purpose).Protect(secret);
            state.SetupExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10); state.LastTimeStep = -1; state.Revision++;
            await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync();
            return Results.Ok(new { secret, recoveryCodes = codes, expiresAt = state.SetupExpiresAt });
        }
        if (action == "enable" && (state.Enabled || user.Role != "Admin" || state.SetupExpiresAt is null || state.SetupExpiresAt <= DateTimeOffset.UtcNow || !request.RecoverySaved)) return Invalid();
        if (action != "enable" && !state.Enabled) return Invalid();
        if (!Proof(state, request.Code, action != "enable"))
        { Failed(state); await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Invalid(); }
        string[]? newCodes = null;
        if (action == "enable") { state.Enabled = true; state.SetupExpiresAt = null; }
        else if (action == "disable") { state.Enabled = false; state.ProtectedSecret = ""; state.RecoveryHashes = []; state.SetupExpiresAt = null; }
        else if (action == "recovery-codes") { newCodes = NewRecoveryCodes(); state.RecoveryHashes = newCodes.Select(Hash).ToArray(); }
        else return Results.NotFound();
        Success(state); ClearChallenge(state); user.TokenVersion++; user.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(user, action == "enable" ? "SecondFactorEnabled" : action == "disable" ? "SecondFactorDisabled" : "RecoveryCodesRenewed");
        await db.SaveChangesAsync(); if (tx is not null) await tx.CommitAsync(); return Results.Ok(Session(user, newCodes));
    }
}
