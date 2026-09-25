using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OvoGrowthOS.Api.Mail;

public sealed class SmtpSettings(IConfiguration configuration)
{
    public bool Enabled => bool.TryParse(configuration["MAIL_ENABLED"], out var enabled) && enabled;
    public string Host => configuration["SMTP_HOST"] ?? "smtp.gmail.com";
    public int Port => int.TryParse(configuration["SMTP_PORT"] ?? "465", out var port) ? port : 0;
    public bool Secure => bool.TryParse(configuration["SMTP_SECURE"] ?? "true", out var secure) && secure;
    public string User => configuration["SMTP_USER"] ?? "";
    public string Password => configuration["SMTP_PASS"] ?? "";
    public string From => configuration["MAIL_FROM"] ?? "";
    public string WebOrigin => (configuration["WebOrigin"] ?? "").TrimEnd('/');
    public bool Configured => Host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase)
        && (Port == 465 && Secure || Port == 587 && !Secure)
        && IsAddress(User) && !string.IsNullOrWhiteSpace(Password)
        && !From.Contains('\r') && !From.Contains('\n') && MailboxAddress.TryParse(From, out _)
        && Uri.TryCreate(WebOrigin, UriKind.Absolute, out var uri) && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == "/"
        && (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback);
    public bool Ready => Enabled && Configured;
    public static bool IsAddress(string? address) => address is { Length: > 0 and <= 320 }
        && !address.Contains('\r') && !address.Contains('\n') && MailboxAddress.TryParse(address, out var parsed)
        && parsed.Address == address;
}

public interface IAccountMailSender
{
    Task SendAsync(Guid deliveryId, string to, string subject, string body, CancellationToken cancellationToken);
}

public interface ISmtpTestSender
{
    Task SendAsync(SmtpSettings settings, string recipient, CancellationToken ct);
}

public sealed class SmtpTestSender : ISmtpTestSender
{
    public Task SendAsync(SmtpSettings settings, string recipient, CancellationToken ct) =>
        SmtpAccountMailSender.SendConfigured(settings, Guid.NewGuid(), recipient, "OVO Growth OS deneme e-postası",
            "Bu ileti, yönetici tarafından e-posta ayarlarını sınamak için gönderildi. Müşteri raporu, hesap bağlantısı veya özel veri içermez. Gelen kutunuza ulaştığını kendiniz kontrol edin.", ct);
}

public sealed class SmtpAccountMailSender(SmtpSettingsProvider provider) : IAccountMailSender
{
    public async Task SendAsync(Guid deliveryId, string to, string subject, string body, CancellationToken cancellationToken)
    {
        var settings = await provider.GetAsync(cancellationToken);
        if (!settings.Ready) throw new InvalidOperationException("Mail is disabled or unconfigured.");
        await SendConfigured(settings, deliveryId, to, subject, body, cancellationToken);
    }

    internal static async Task SendConfigured(SmtpSettings settings, Guid deliveryId, string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (!settings.Configured) throw new InvalidOperationException("Mail is unconfigured.");
        var message = new MimeMessage { Subject = subject, MessageId = $"{deliveryId:N}@ovo-growth-os", Body = new TextPart("plain") { Text = body } };
        message.From.Add(MailboxAddress.Parse(settings.From));
        message.To.Add(MailboxAddress.Parse(to)); // MAIL_TO is deliberately never used for private account links.
        using var client = new SmtpClient { Timeout = 15000 };
        await client.ConnectAsync(settings.Host, settings.Port, settings.Secure ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(settings.User, settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        // SMTP acceptance is the success boundary; disconnect failure must not turn it into a retry.
        try { await client.DisconnectAsync(true, cancellationToken); } catch { /* Already accepted by SMTP. */ }
    }
}
