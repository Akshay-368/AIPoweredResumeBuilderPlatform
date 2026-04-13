using System.Text.Json.Serialization;

namespace ResumeAI.Auth.API.DTOs;

public record RefreshDto(
    [property: JsonPropertyName("refreshToken")] string RefreshToken
);