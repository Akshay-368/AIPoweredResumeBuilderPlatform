using System.Text.Json;

namespace ResumeAI.ATSScore.API.DTO.Projects;

public record CreateProjectRequestDto(
    string Name,
    string Type,
    string? Status = null,
    int? CurrentStep = null
);

public record UpdateProjectRequestDto(
    string? Name = null,
    string? Type = null,
    string? Status = null,
    int? CurrentStep = null
);

public record ProjectResponseDto(
    Guid ProjectId,
    int UserId,
    string Name,
    string Type,
    string Status,
    int CurrentStep,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record UpsertResumeArtifactRequestDto(
    string? RawText,
    JsonElement ParsedResumeJson,
    string SourceType = "upload"
);

public record UpsertJdArtifactRequestDto(
    string? RawText,
    JsonElement ParsedJdJson,
    string SourceType = "upload"
);

public record UpsertAtsResultRequestDto(
    string JobRole,
    string? CustomRole,
    JsonElement AtsResultJson,
    int OverallScore
);

public record UpsertWizardStateRequestDto(
    int CurrentStep,
    JsonElement StateJson
);
