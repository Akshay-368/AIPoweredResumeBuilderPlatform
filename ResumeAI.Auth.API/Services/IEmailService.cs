namespace ResumeAI.Auth.API.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken);
}
