namespace ResumeAI.Auth.API.DTOs;

/// <summary>
/// Login request payload containing user credentials.
/// </summary>
public record LoginDto(string? Email, string Password, string? PhoneNumber = null);