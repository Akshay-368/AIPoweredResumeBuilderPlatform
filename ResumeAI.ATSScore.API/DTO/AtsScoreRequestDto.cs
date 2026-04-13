using ResumeAI.ATSScore.API.Models;

namespace ResumeAI.ATSScore.API.DTO;

/// <summary>
/// Request to score a resume against a job description.
/// </summary>
public record AtsScoreRequestDto(
    ResumeData ResumeData,
    string JobDescriptionText,
    string JobRole,
    string? CustomRole = null,
    string? ProjectId = null
);
