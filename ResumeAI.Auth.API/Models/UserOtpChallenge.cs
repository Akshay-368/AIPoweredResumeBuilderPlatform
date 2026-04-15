using System.ComponentModel.DataAnnotations;

namespace ResumeAI.Auth.API.Models;

public class UserOtpChallenge
{
    [Key]
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string OtpHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public bool IsConsumed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public User User { get; set; } = null!;
}
