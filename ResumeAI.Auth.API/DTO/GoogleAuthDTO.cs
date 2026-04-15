namespace ResumeAI.Auth.API.DTOs;

/// <summary>
/// Google ID token payload for OAuth sign-in/up.
/// </summary>
public record GoogleAuthDto(
    string IdToken,
    string? Role = "user",
    string? AdminSecretKey = null
);
