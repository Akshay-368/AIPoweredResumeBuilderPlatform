using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ResumeAI.Auth.API.Services;

public class MailKitEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MailKitEmailService> _logger;

    public MailKitEmailService(IConfiguration configuration, ILogger<MailKitEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken)
    {
        var smtpHost = _configuration["Smtp:Host"] ?? Environment.GetEnvironmentVariable("Smtp__Host");
        var smtpPortText = _configuration["Smtp:Port"] ?? Environment.GetEnvironmentVariable("Smtp__Port");
        var smtpUser = _configuration["Smtp:Username"] ?? Environment.GetEnvironmentVariable("Smtp__Username");
        var smtpPass = _configuration["Smtp:Password"] ?? Environment.GetEnvironmentVariable("Smtp__Password");
        var smtpFrom = _configuration["Smtp:FromEmail"] ?? Environment.GetEnvironmentVariable("Smtp__FromEmail");
        var secureSocket = _configuration["Smtp:SecureSocketOption"] ?? Environment.GetEnvironmentVariable("Smtp__SecureSocketOption") ?? "StartTls";

        if (string.IsNullOrWhiteSpace(smtpHost)
            || !int.TryParse(smtpPortText, out var smtpPort)
            || string.IsNullOrWhiteSpace(smtpUser)
            || string.IsNullOrWhiteSpace(smtpPass)
            || string.IsNullOrWhiteSpace(smtpFrom))
        {
            throw new InvalidOperationException("SMTP configuration is missing. Configure Smtp__Host, Smtp__Port, Smtp__Username, Smtp__Password, and Smtp__FromEmail.");
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtpFrom));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = textBody };

        var secureSocketOptions = secureSocket.ToLowerInvariant() switch
        {
            "none" => SecureSocketOptions.None,
            "ssl" or "ssltls" => SecureSocketOptions.SslOnConnect,
            "starttlswhenavailable" => SecureSocketOptions.StartTlsWhenAvailable,
            _ => SecureSocketOptions.StartTls
        };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(smtpHost, smtpPort, secureSocketOptions, cancellationToken);
        await smtp.AuthenticateAsync(smtpUser, smtpPass, cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("OTP mail sent to {ToEmail}", toEmail);
    }
}
