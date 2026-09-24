using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OvoGrowthOS.Api.Auth;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Features;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Tests;

public sealed class AccountMailTests
{
    public sealed class Sender : IAccountMailSender
    {
        public List<(string To, string Body)> Messages { get; } = [];
        public bool Fail { get; set; }
        public Task SendAsync(Guid id, string to, string subject, string body, CancellationToken ct)
        {
            Messages.Add((to, body));
            if (Fail) throw new IOException("Pretend ambiguous SMTP failure; must not be persisted.");
            return Task.CompletedTask;
        }
    }
    private static WebApplicationFactory<Program> Configured(WorkflowApiFactory parent, Sender sender, bool enabled = true) => parent.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["MAIL_ENABLED"] = enabled.ToString(), ["SMTP_HOST"] = "smtp.gmail.com", ["SMTP_PORT"] = "465", ["SMTP_SECURE"] = "true", ["SMTP_USER"] = "sender@example.test", ["SMTP_PASS"] = "test-only-not-a-real-password",
            ["MAIL_FROM"] = "OVO <sender@example.test>", ["MAIL_TO"] = "must-not-receive@example.test", ["WebOrigin"] = "https://panel.example.test" }));
        builder.ConfigureServices(s => { s.RemoveAll<IAccountMailSender>(); s.AddSingleton<IAccountMailSender>(sender); });
    });
    private static async Task<HttpClient> Admin(WebApplicationFactory<Program> f, string role = "admin")
    {
        var c=f.CreateClient(); var r=await c.PostAsJsonAsync("/api/auth/login",new{email=role+"@ovo.test",password=WorkflowApiFactory.TestPassword});r.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Authorization=new("Bearer",(await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());return c;
    }
    private static async Task Db(WebApplicationFactory<Program> f, Func<AppDbContext,Task> action)
    { await using var scope=f.Services.CreateAsyncScope();await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
    private static async Task<bool> Process(WebApplicationFactory<Program> f)
    { await using var scope=f.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<AccountMailQueue>().ProcessOne(); }
    private static string Token(Sender s, int index=0)=>Regex.Match(s.Messages[index].Body,@"#token=([A-F0-9]{64})").Groups[1].Value;
    private static AccountInvitationRequest Invite(string role="Analyst",Guid? brand=null)=>new("new@example.test","Yeni kişi",role,brand);

    [Fact]
    public async Task Disabled_mail_creates_no_account_or_token_and_public_response_is_generic()
    {
        await using var parent=new WorkflowApiFactory();var sender=new Sender();await using var f=Configured(parent,sender,false);using var admin=await Admin(f);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await admin.PostAsJsonAsync("/api/account-mail/invitations",Invite())).StatusCode);
        using var c=f.CreateClient();var known=await (await c.PostAsJsonAsync("/api/auth/forgot-password",new{email="admin@ovo.test"})).Content.ReadAsStringAsync();
        var unknown=await (await c.PostAsJsonAsync("/api/auth/forgot-password",new{email="unknown@example.test"})).Content.ReadAsStringAsync();Assert.Equal(known,unknown);
        await Db(f,async db=>{Assert.Empty(await db.AccountLinks.ToListAsync());Assert.Equal(3,await db.UserAccounts.CountAsync());});Assert.False(await Process(f));Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Invitation_is_private_single_use_and_only_real_recipient_gets_a_link()
    {
        await using var parent=new WorkflowApiFactory();var sender=new Sender();await using var f=Configured(parent,sender);using var admin=await Admin(f);
        (await admin.PostAsJsonAsync("/api/account-mail/invitations",Invite())).EnsureSuccessStatusCode();
        await Db(f,async db=>{Assert.True((await db.UserAccounts.SingleAsync(x=>x.Email=="new@example.test")).InvitationPending);var m=await db.MailDeliveries.SingleAsync();Assert.DoesNotContain("#token=",m.ProtectedBody);});
        Assert.True(await Process(f));Assert.False(await Process(f));Assert.Single(sender.Messages);Assert.Equal("new@example.test",sender.Messages[0].To);
        Assert.DoesNotContain(WorkflowApiFactory.TestPassword,sender.Messages[0].Body);var token=Token(sender);Assert.Equal(64,token.Length);
        var listing=await admin.GetStringAsync("/api/account-mail/deliveries");Assert.DoesNotContain(token,listing);Assert.DoesNotContain("protectedBody",listing);Assert.Contains("Sent",listing);
        using var c=f.CreateClient();var body=new CompleteAccountLinkRequest(token,"New-valid-password-2026!");
        (await c.PostAsJsonAsync("/api/auth/complete-account",body)).EnsureSuccessStatusCode();Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/auth/complete-account",body)).StatusCode);
        (await c.PostAsJsonAsync("/api/auth/login",new{email="new@example.test",password=body.Password})).EnsureSuccessStatusCode();
        await Db(f,async db=>{Assert.Equal("",(await db.MailDeliveries.SingleAsync()).ProtectedBody);Assert.NotNull((await db.AccountLinks.SingleAsync()).UsedAt);Assert.False((await db.UserAccounts.SingleAsync(x=>x.Email=="new@example.test")).InvitationPending);});
    }

    [Fact]
    public async Task Reset_invalidates_existing_sessions_and_never_changes_role()
    {
        await using var parent=new WorkflowApiFactory();var sender=new Sender();await using var f=Configured(parent,sender);using var old=await Admin(f);using var c=f.CreateClient();
        (await c.PostAsJsonAsync("/api/auth/forgot-password",new{email="admin@ovo.test"})).EnsureSuccessStatusCode();await Process(f);
        (await c.PostAsJsonAsync("/api/auth/complete-account",new CompleteAccountLinkRequest(Token(sender),"Reset-valid-password-2026!"))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized,(await old.GetAsync("/api/auth/me")).StatusCode);
        await Db(f,async db=>{var a=await db.UserAccounts.SingleAsync(x=>x.Email=="admin@ovo.test");Assert.Equal("Admin",a.Role);Assert.Equal(1,a.TokenVersion);Assert.Contains(await db.AuditRecords.ToListAsync(),x=>x.Action=="AccountPasswordReset");});
    }

    [Theory]
    [InlineData("expired")][InlineData("closed")][InlineData("changed")]
    public async Task Expired_closed_or_changed_accounts_cannot_use_links(string scenario)
    {
        await using var parent=new WorkflowApiFactory();var sender=new Sender();await using var f=Configured(parent,sender);using var c=f.CreateClient();
        (await c.PostAsJsonAsync("/api/auth/forgot-password",new{email="partner@ovo.test"})).EnsureSuccessStatusCode();await Process(f);
        await Db(f,async db=>{var link=await db.AccountLinks.SingleAsync();var a=await db.UserAccounts.SingleAsync(x=>x.Id==link.UserId);if(scenario=="expired")link.ExpiresAt=DateTimeOffset.UtcNow.AddSeconds(-1);else if(scenario=="closed")a.IsActive=false;else a.TokenVersion++;await db.SaveChangesAsync();});
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/auth/complete-account",new CompleteAccountLinkRequest(Token(sender),"Valid-new-password-2026!"))).StatusCode);
    }

    [Fact]
    public async Task Unknown_and_closed_accounts_match_public_answer_and_throttling_does_not_duplicate_mail()
    {
        await using var parent=new WorkflowApiFactory();var sender=new Sender();await using var f=Configured(parent,sender);using var c=f.CreateClient();
        var known=await (await c.PostAsJsonAsync("/api/auth/forgot-password",new{email="partner@ovo.test"})).Content.ReadAsStringAsync();
        var again=await (await c.PostAsJsonAsync("/api/auth/forgot-password",new{email="partner@ovo.test"})).Content.ReadAsStringAsync();
        var unknown=await (await c.PostAsJsonAsync("/api/auth/forgot-password",new{email="nobody@example.test"})).Content.ReadAsStringAsync();Assert.Equal(known,again);Assert.Equal(known,unknown);
        await Db(f,async db=>{Assert.Single(await db.MailDeliveries.ToListAsync());var a=await db.UserAccounts.SingleAsync(x=>x.Email=="partner@ovo.test");a.IsActive=false;await db.SaveChangesAsync();});
        Assert.True(await Process(f));Assert.Empty(sender.Messages);await Db(f,async db=>Assert.Equal(MailDeliveryStatus.Cancelled,(await db.MailDeliveries.SingleAsync()).Status));
    }

    [Fact]
    public async Task Ambiguous_smtp_failure_is_visible_and_never_automatically_retried()
    {
        await using var parent=new WorkflowApiFactory();var sender=new Sender{Fail=true};await using var f=Configured(parent,sender);using var c=f.CreateClient();
        (await c.PostAsJsonAsync("/api/auth/forgot-password",new{email="partner@ovo.test"})).EnsureSuccessStatusCode();await Process(f);Assert.False(await Process(f));Assert.Single(sender.Messages);
        await Db(f,async db=>{var mail=await db.MailDeliveries.SingleAsync();Assert.Equal(MailDeliveryStatus.Uncertain,mail.Status);Assert.Equal("SmtpNotConfirmed",mail.ErrorCode);Assert.Empty(mail.ProtectedBody);});
    }

    [Fact]
    public async Task Renewed_invitation_invalidates_old_link_and_pending_admin_cannot_replace_last_admin()
    {
        await using var parent=new WorkflowApiFactory();var sender=new Sender();await using var f=Configured(parent,sender);using var admin=await Admin(f);
        var r=await admin.PostAsJsonAsync("/api/account-mail/invitations",Invite("Admin"));r.EnsureSuccessStatusCode();var id=(await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();await Process(f);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PutAsJsonAsync($"/api/users/{WorkflowApiFactory.AccountId("admin@ovo.test")}",new UserAccountRequest("admin@ovo.test","Admin","Analyst",null))).StatusCode);
        await Db(f,async db=>{(await db.AccountLinks.SingleAsync()).CreatedAt=DateTimeOffset.UtcNow.AddMinutes(-2);await db.SaveChangesAsync();});
        (await admin.PostAsJsonAsync($"/api/account-mail/invitations/{id}/resend",new{})).EnsureSuccessStatusCode();await Process(f);
        using var c=f.CreateClient();Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/auth/complete-account",new CompleteAccountLinkRequest(Token(sender),"New-password-2026!"))).StatusCode);
        (await c.PostAsJsonAsync("/api/auth/complete-account",new CompleteAccountLinkRequest(Token(sender,1),"New-password-2026!"))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Portal_invitation_binds_to_brand_and_non_admin_cannot_invite_or_read_mail()
    {
        await using var parent=new WorkflowApiFactory();var sender=new Sender();await using var f=Configured(parent,sender);using var admin=await Admin(f);using var partner=await Admin(f,"partner");
        var brand=new Brand{Name="Invitation test"};await Db(f,async db=>{db.Brands.Add(brand);await db.SaveChangesAsync();});
        Assert.Equal(HttpStatusCode.Forbidden,(await partner.PostAsJsonAsync("/api/account-mail/invitations",Invite())).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await partner.GetAsync("/api/account-mail/deliveries")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync("/api/account-mail/invitations",Invite("BrandClient"))).StatusCode);
        (await admin.PostAsJsonAsync("/api/account-mail/invitations",Invite("BrandClient",brand.Id))).EnsureSuccessStatusCode();await Process(f);
        using var c=f.CreateClient();(await c.PostAsJsonAsync("/api/auth/complete-account",new CompleteAccountLinkRequest(Token(sender),"Client-password-2026!"))).EnsureSuccessStatusCode();
        await Db(f,async db=>{Assert.Equal(brand.Id,(await db.PortalAccesses.SingleAsync()).BrandId);});
    }

    [Fact]
    public async Task Interrupted_delivery_is_not_sent_again_and_missing_key_is_visible()
    {
        await using var parent=new WorkflowApiFactory();var sender=new Sender();await using var f=Configured(parent,sender);using var c=f.CreateClient();
        (await c.PostAsJsonAsync("/api/auth/forgot-password",new{email="partner@ovo.test"})).EnsureSuccessStatusCode();
        await Db(f,async db=>{var m=await db.MailDeliveries.SingleAsync();m.Status=MailDeliveryStatus.Sending;m.AttemptedAt=DateTimeOffset.UtcNow.AddMinutes(-6);await db.SaveChangesAsync();});
        Assert.False(await Process(f));Assert.Empty(sender.Messages);await Db(f,async db=>{var m=await db.MailDeliveries.SingleAsync();Assert.Equal(MailDeliveryStatus.Uncertain,m.Status);Assert.Equal("Interrupted",m.ErrorCode);});
        (await c.PostAsJsonAsync("/api/auth/forgot-password",new{email="analyst@ovo.test"})).EnsureSuccessStatusCode();
        await Db(f,async db=>{var m=await db.MailDeliveries.SingleAsync(x=>x.Status==MailDeliveryStatus.Pending);m.ProtectedBody="unreadable";await db.SaveChangesAsync();});await Process(f);Assert.Empty(sender.Messages);
        await Db(f,async db=>Assert.Contains(await db.MailDeliveries.ToListAsync(),x=>x.ErrorCode=="ProtectionKeyUnavailable"));
    }

    [Theory]
    [InlineData("465","true",true)][InlineData("587","false",true)][InlineData("25","false",false)][InlineData("465","false",false)][InlineData("0","true",false)]
    public void Gmail_configuration_requires_encrypted_transport(string port,string secure,bool valid)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["MAIL_ENABLED"]="true",["SMTP_PORT"]=port,["SMTP_SECURE"]=secure,["SMTP_USER"]="from@example.test",["SMTP_PASS"]="not-real",["MAIL_FROM"]="OVO <from@example.test>",["WebOrigin"]="https://panel.example.test"}).Build();
        Assert.Equal(valid,new SmtpSettings(config).Ready);config["WebOrigin"]="https://panel.example.test/?redirect=evil";Assert.False(new SmtpSettings(config).Ready);
    }
}
