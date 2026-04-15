using System.Text.Json;

namespace ResumeAI.ATSScore.API.DTO.ResumeBuilder;

public record ResumeBuilderBasicInfoDto(
    string FullName,
    string ProfessionalRole,
    string Email,
    string? Phone = null,
    string? LinkedInUrl = null,
    string? PortfolioUrl = null,
    string? Location = null,
    string? Summary = null
);

public record ResumeBuilderTargetJobDto(
    string? Role = null,
    string? CustomRole = null,
    string? JobDescriptionText = null
);

public record ResumeBuilderEducationDto(
    string Institution,
    string Degree,
    string? FieldOfStudy = null,
    string? StartYear = null,
    string? EndYear = null,
    bool IsPresent = false,
    string? Marks = null
);

public record ResumeBuilderExperienceDto(
    string Company,
    string Role,
    string? StartDate = null,
    string? EndDate = null,
    bool IsPresent = false,
    string? Description = null
);

public record ResumeBuilderProjectDto(
    string Name,
    string TechStack,
    string? Description = null
);

public record ResumeBuilderWizardSnapshotDto(
    ResumeBuilderBasicInfoDto BasicInfo,
    ResumeBuilderTargetJobDto TargetJob,
    List<ResumeBuilderEducationDto> Education,
    List<ResumeBuilderExperienceDto> Experience,
    List<ResumeBuilderProjectDto> Projects,
    bool NoPriorExperience = false
);

public record ResumeBuilderRevisionContextDto(
    JsonElement CurrentPreviewJson,
    string UserChangeRequest
);

public record ResumeBuilderGenerateRequestDto(
    string ProjectId,
    string TemplateId,
    ResumeBuilderWizardSnapshotDto WizardSnapshot,
    JsonElement? PrefilledResumeJson = null,
    string? TargetRole = null,
    string? Tone = null,
    string? LengthPolicy = null,
    ResumeBuilderRevisionContextDto? RevisionContext = null
);

public record ResumeBuilderPdfExportRequestDto(
    string ProjectId,
    string TemplateId,
    JsonElement? ResumeJson = null,
    JsonElement? RenderOptions = null
);

public record ResumeBuilderTemplateDto(
    string TemplateId,
    string Title,
    string Description,
    string Category,
    string? PreviewThumbnailBase64,
    string? AssetGroupKey,
    JsonElement RenderContractJson,
    JsonElement StyleGuideJson,
    bool IsDefault
);

public record ResumeBuilderArtifactResponseDto(
    Guid ArtifactId,
    Guid ProjectId,
    string TemplateId,
    JsonElement BuilderSnapshotJson,
    JsonElement GeneratedResumeJson,
    string GenerationModel,
    int RevisionCount,
    bool IsFinalized,
    DateTime? FinalizedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ResumeBuilderPdfExportResponseDto(
    Guid ExportId,
    Guid ProjectId,
    Guid? ArtifactId,
    string TemplateId,
    string FileName,
    DateTime CreatedAt
);

public record ResumeBuilderGenerationResult(
    ResumeBuilderArtifactResponseDto Artifact,
    string ModelUsed
);