namespace ResumeAI.Auth.API.DTOs;

/// <summary>
/// Current authenticated user profile projection.
/// </summary>
public record UserProfileDto(
    int UserId,
    string FullName,
    string Email,
    string PhoneNumber,
    string Role,
    string Provider
);

/// <summary>
/// Request payload to update current user's phone number.
/// </summary>
public record UpdatePhoneNumberDto(string PhoneNumber);
