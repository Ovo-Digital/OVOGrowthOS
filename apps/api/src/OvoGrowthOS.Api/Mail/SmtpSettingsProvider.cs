using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Mail;

public sealed class SmtpSettingsProvider(AppDbContext db, IConfiguration configuration, IDataProtectionProvider protection)
{
    private const string Purpose = "OVO.SmtpCredentials.v1";
    public bool ForceDisabled => bool.TryParse(configuration["MAIL_FORCE_DISABLED"], out var disabled) && disabled;
    public string Protect(string password) => protection.CreateProtector(Purpose).Protect(password);
    public string Unprotect(string password) => protection.CreateProtector(Purpose).Unprotect(password);

    public async Task<SmtpSettings> GetAsync(CancellationToken ct = default)
    {
        var saved = await db.MailConfigurations.AsNoTracking().SingleOrDefaultAsync(ct);
        if (saved is not null) return FromSaved(saved);
        // Environment settings remain compatible until the first panel save. Never copy their secret to a response.
        if (!ForceDisabled) return new SmtpSettings(configuration);
        return new SmtpSettings(new ConfigurationBuilder().AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MAIL_ENABLED"] = "false" }).Build());
    }

    public SmtpSettings FromSaved(MailConfiguration saved)
    {
        var password = "";
        try { if (saved.ProtectedPassword.Length > 0) password = Unprotect(saved.ProtectedPassword); }
        catch (System.Security.Cryptography.CryptographicException) { /* Fail closed; do not fall back to an old server password. */ }
        return new SmtpSettings(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MAIL_ENABLED"] = (saved.Enabled && !ForceDisabled).ToString(), ["SMTP_HOST"] = saved.Host,
            ["SMTP_PORT"] = saved.Port.ToString(), ["SMTP_SECURE"] = saved.Secure.ToString(), ["SMTP_USER"] = saved.User,
            ["SMTP_PASS"] = password, ["MAIL_FROM"] = SmtpSettings.IsAddress(saved.FromAddress) ? new MailboxAddress(saved.FromName, saved.FromAddress).ToString() : "",
            ["WebOrigin"] = configuration["WebOrigin"]
        }).Build());
    }
}
