using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResumeAI.Auth.API.Models;

/// <summary>
/// Represents an authenticated user account in the system.
/// </summary>
public class User
{
    /// <summary>
    /// Primary key for the user record.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// User's display name.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Unique email address and phone number used for login and communication.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    [Column(TypeName = "varchar(30)")] // just for future use as of now it's only 10 digits but in future we can have country code as well
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits.")]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Hashed password value (never plain text).
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Authorization role assigned to the user (for example: USER, ADMIN).
    /// </summary>
    public string Role { get; set; } = "USER";

    /// <summary>
    /// Active subscription tier (for example: FREE, PREMIUM).
    /// </summary>
    public string SubscriptionPlan { get; set; } = "FREE";

    /// <summary>
    /// UTC timestamp when the account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
    public string Provider { get; set; } = "LOCAL";
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }

    public ICollection<UserOtpChallenge> OtpChallenges { get; set; } = new List<UserOtpChallenge>();
}