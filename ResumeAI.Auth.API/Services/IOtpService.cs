using ResumeAI.Auth.API.Models;

namespace ResumeAI.Auth.API.Services;

public interface IOtpService
{
    Task<(UserOtpChallenge Challenge, string Otp)> CreateChallengeAsync(User user, string purpose, TimeSpan ttl, CancellationToken cancellationToken);
    Task<(bool IsValid, string Error)> ValidateAsync(User user, string purpose, string otp, int maxAttempts, CancellationToken cancellationToken);
    Task<(bool IsValid, string Error)> ValidateAndConsumeAsync(User user, string purpose, string otp, int maxAttempts, CancellationToken cancellationToken);
}
