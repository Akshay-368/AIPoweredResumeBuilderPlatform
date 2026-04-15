using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ResumeAI.Auth.API.Data;
using ResumeAI.Auth.API.Models;

namespace ResumeAI.Auth.API.Services;

public class OtpService : IOtpService
{
    private readonly AuthDbContext _db;
    private readonly IConfiguration _configuration;

    public OtpService(AuthDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<(UserOtpChallenge Challenge, string Otp)> CreateChallengeAsync(User user, string purpose, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var challenge = new UserOtpChallenge
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            Purpose = purpose,
            OtpHash = BuildOtpHash(otp),
            ExpiresAt = DateTime.UtcNow.Add(ttl),
            AttemptCount = 0,
            IsConsumed = false,
            CreatedAt = DateTime.UtcNow,
            ConsumedAt = null
        };

        var oldChallenges = await _db.UserOtpChallenges
            .Where(x => x.UserId == user.UserId && x.Purpose == purpose && !x.IsConsumed)
            .ToListAsync(cancellationToken);

        foreach (var old in oldChallenges)
        {
            old.IsConsumed = true;
            old.ConsumedAt = DateTime.UtcNow;
        }

        _db.UserOtpChallenges.Add(challenge);
        await _db.SaveChangesAsync(cancellationToken);

        return (challenge, otp);
    }

    public async Task<(bool IsValid, string Error)> ValidateAndConsumeAsync(User user, string purpose, string otp, int maxAttempts, CancellationToken cancellationToken)
    {
        var validation = await ValidateInternalAsync(user, purpose, otp, maxAttempts, consumeOnSuccess: true, cancellationToken);
        return validation;
    }

    public async Task<(bool IsValid, string Error)> ValidateAsync(User user, string purpose, string otp, int maxAttempts, CancellationToken cancellationToken)
    {
        var validation = await ValidateInternalAsync(user, purpose, otp, maxAttempts, consumeOnSuccess: false, cancellationToken);
        return validation;
    }

    private async Task<(bool IsValid, string Error)> ValidateInternalAsync(User user, string purpose, string otp, int maxAttempts, bool consumeOnSuccess, CancellationToken cancellationToken)
    {
        var challenge = await _db.UserOtpChallenges
            .Where(x => x.UserId == user.UserId && x.Purpose == purpose && !x.IsConsumed)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge is null)
        {
            return (false, "OTP challenge not found.");
        }

        if (challenge.ExpiresAt <= DateTime.UtcNow)
        {
            challenge.IsConsumed = true;
            challenge.ConsumedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return (false, "OTP has expired.");
        }

        if (challenge.AttemptCount >= maxAttempts)
        {
            challenge.IsConsumed = true;
            challenge.ConsumedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return (false, "OTP attempts exceeded.");
        }

        var incomingHash = BuildOtpHash(otp);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(challenge.OtpHash), Encoding.UTF8.GetBytes(incomingHash)))
        {
            challenge.AttemptCount += 1;
            if (challenge.AttemptCount >= maxAttempts)
            {
                challenge.IsConsumed = true;
                challenge.ConsumedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return (false, "Invalid OTP.");
        }

        if (consumeOnSuccess)
        {
            challenge.IsConsumed = true;
            challenge.ConsumedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return (true, string.Empty);
    }

    private string BuildOtpHash(string otp)
    {
        var secret = _configuration["Otp:Secret"]
            ?? Environment.GetEnvironmentVariable("Otp__Secret")
            ?? _configuration["Jwt:Key"]
            ?? Environment.GetEnvironmentVariable("Jwt__Key")
            ?? "resumeai-fallback-otp-secret";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(otp));
        return Convert.ToBase64String(hash);
    }
}
