namespace ResumeAI.Auth.API.DTOs;

/// <summary>
/// Registration request payload used to create a new local user account.
/// </summary>
public record RegisterDto(
	string FullName,
	string Email,
	string Password,
	string PhoneNumber,
	string Role = "user",
	string? AdminSecretKey = null
);