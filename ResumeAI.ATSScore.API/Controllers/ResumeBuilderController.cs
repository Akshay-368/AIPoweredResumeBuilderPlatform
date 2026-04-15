using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeAI.ATSScore.API.Data;
using ResumeAI.ATSScore.API.DTO.ResumeBuilder;
using ResumeAI.ATSScore.API.Models;
using ResumeAI.ATSScore.API.Persistence;
using ResumeAI.ATSScore.API.Services;
using System.Security.Claims;
using System.Text.Json;

namespace ResumeAI.ATSScore.API.Controllers;

[ApiController]
[Authorize]
public class ResumeBuilderController : ControllerBase
{
    private readonly ProjectsDbContext _db;
    private readonly ResumeBuilderGeminiService _geminiService;
    private readonly ResumeBuilderPdfService _pdfService;
    private readonly ILogger<ResumeBuilderController> _logger;

    public ResumeBuilderController(
        ProjectsDbContext db,
        ResumeBuilderGeminiService geminiService,
        ResumeBuilderPdfService pdfService,
        ILogger<ResumeBuilderController> logger)
    {
        _db = db;
        _geminiService = geminiService;
        _pdfService = pdfService;
        _logger = logger;
    }

    [HttpGet("api/projects/resume-builder/templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        var templates = await _db.ResumeBuilderTemplates
            .Where(template => template.IsActive)
            .OrderByDescending(template => template.IsDefault)
            .ThenBy(template => template.Title)
            .ToListAsync(cancellationToken);

        return Ok(templates.Select(template => new ResumeBuilderTemplateDto(
            template.TemplateId,
            template.Title,
            template.Description,
            template.Category,
            template.PreviewThumbnailBase64,
            template.AssetGroupKey,
            DeserializeJson(template.RenderContractJson),
            DeserializeJson(template.StyleGuideJson),
            template.IsDefault)));
    }

    [HttpGet("api/projects/{projectId:guid}/resume-builder/artifact")]
    public async Task<IActionResult> GetArtifact([FromRoute] Guid projectId, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var artifact = await GetLatestArtifactEntityAsync(projectId, cancellationToken);
        if (artifact is null)
        {
            return NotFound(new { message = "Resume preview artifact not found." });
        }

        return Ok(ToArtifactResponse(artifact));
    }

    [HttpPost("api/projects/{projectId:guid}/resume-builder/generate")]
    public Task<IActionResult> Generate([FromRoute] Guid projectId, [FromBody] ResumeBuilderGenerateRequestDto request, CancellationToken cancellationToken)
    {
        return GenerateInternalAsync(projectId, request, cancellationToken);
    }

    [HttpPost("api/projects/{projectId:guid}/resume-builder/revise")]
    public Task<IActionResult> Revise([FromRoute] Guid projectId, [FromBody] ResumeBuilderGenerateRequestDto request, CancellationToken cancellationToken)
    {
        return GenerateInternalAsync(projectId, request, cancellationToken);
    }

    [HttpPost("api/projects/{projectId:guid}/resume-builder/export-pdf")]
    public async Task<IActionResult> ExportPdf([FromRoute] Guid projectId, [FromBody] ResumeBuilderPdfExportRequestDto request, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectId) && !string.Equals(request.ProjectId, projectId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Project id in the request body does not match the route." });
        }

        var artifact = await GetLatestArtifactEntityAsync(projectId, cancellationToken);
        if (artifact is null)
        {
            return BadRequest(new { message = "Generate a resume preview before exporting PDF." });
        }

        if (!artifact.IsFinalized)
        {
            artifact.IsFinalized = true;
            artifact.FinalizedAt = DateTime.UtcNow;
        }

        var templateId = string.IsNullOrWhiteSpace(request.TemplateId) ? artifact.TemplateId : request.TemplateId;
        var template = await ResolveTemplateOrDefaultAsync(templateId, cancellationToken);

        var resume = request.ResumeJson.HasValue && request.ResumeJson.Value.ValueKind != JsonValueKind.Undefined
            ? Deserialize<ResumeBuilderGeneratedResumeDto>(request.ResumeJson.Value.GetRawText())
            : Deserialize<ResumeBuilderGeneratedResumeDto>(artifact.GeneratedResumeJson);
        if (resume is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Stored preview JSON could not be read." });
        }

        var pdfBytes = await _pdfService.RenderAsync(resume, template, cancellationToken);
        var sha256 = ResumeBuilderPdfService.BuildSha256(pdfBytes);
        var fileName = BuildPdfFileName(resume.Profile.FullName, template.Title);

        var export = new ResumePdfExportEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ArtifactId = artifact.Id,
            TemplateId = template.TemplateId,
            RenderOptionsJson = SerializeRenderOptions(request.RenderOptions),
            PdfBytes = pdfBytes,
            Sha256 = sha256,
            FileName = fileName,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.ResumePdfExports.Add(export);
        artifact.UpdatedAt = DateTime.UtcNow;
        project.CurrentStep = Math.Max(project.CurrentStep, 7);
        project.Status = "completed";
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpPost("api/projects/{projectId:guid}/resume-builder/preview-pdf")]
    public async Task<IActionResult> PreviewPdf([FromRoute] Guid projectId, [FromBody] ResumeBuilderPdfExportRequestDto request, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectId) && !string.Equals(request.ProjectId, projectId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Project id in the request body does not match the route." });
        }

        var artifact = await GetLatestArtifactEntityAsync(projectId, cancellationToken);

        var templateId = !string.IsNullOrWhiteSpace(request.TemplateId)
            ? request.TemplateId
            : artifact?.TemplateId;

        var template = await ResolveTemplateOrDefaultAsync(templateId, cancellationToken);

        var resume = request.ResumeJson.HasValue && request.ResumeJson.Value.ValueKind != JsonValueKind.Undefined
            ? Deserialize<ResumeBuilderGeneratedResumeDto>(request.ResumeJson.Value.GetRawText())
            : artifact is null
                ? null
                : Deserialize<ResumeBuilderGeneratedResumeDto>(artifact.GeneratedResumeJson);

        if (resume is null)
        {
            return BadRequest(new { message = "Resume JSON is required to render preview." });
        }

        var pdfBytes = await _pdfService.RenderAsync(resume, template, cancellationToken);
        return File(pdfBytes, "application/pdf");
    }

    [HttpGet("api/projects/{projectId:guid}/resume-builder/pdf/latest/metadata")]
    public async Task<IActionResult> GetLatestPdfMetadata([FromRoute] Guid projectId, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var latest = await _db.ResumePdfExports
            .Where(export => export.ProjectId == projectId && !export.IsDeleted)
            .OrderByDescending(export => export.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return NotFound(new { message = "PDF export not found." });
        }

        return Ok(new ResumeBuilderPdfExportResponseDto(
            latest.Id,
            latest.ProjectId,
            latest.ArtifactId,
            latest.TemplateId,
            latest.FileName,
            latest.CreatedAt));
    }

    [HttpGet("api/projects/{projectId:guid}/resume-builder/pdf/latest")]
    public async Task<IActionResult> GetLatestPdf([FromRoute] Guid projectId, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var latest = await _db.ResumePdfExports
            .Where(export => export.ProjectId == projectId && !export.IsDeleted)
            .OrderByDescending(export => export.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return NotFound(new { message = "PDF export not found." });
        }

        return File(latest.PdfBytes, "application/pdf", latest.FileName);
    }

    private async Task<IActionResult> GenerateInternalAsync(Guid projectId, ResumeBuilderGenerateRequestDto request, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectId) && !string.Equals(request.ProjectId, projectId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Project id in the request body does not match the route." });
        }

        if (request.WizardSnapshot is null)
        {
            return BadRequest(new { message = "Resume builder snapshot is required." });
        }

        var template = await ResolveTemplateOrDefaultAsync(request.TemplateId, cancellationToken);

        var (modelUsed, resume) = await ResolveResumePayloadAsync(template, request, cancellationToken);
        if (resume is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Unable to build resume payload for preview generation." });
        }

        var now = DateTime.UtcNow;
        var artifact = await GetLatestArtifactEntityAsync(projectId, cancellationToken);
        var snapshotJson = SanitizeJson(request.WizardSnapshot);
        var generatedJson = SanitizeJson(resume);

        if (artifact is null)
        {
            artifact = new ResumeBuilderArtifactEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                TemplateId = template.TemplateId,
                BuilderSnapshotJson = snapshotJson,
                GeneratedResumeJson = generatedJson,
                GenerationModel = modelUsed,
                LastChangeRequest = request.RevisionContext?.UserChangeRequest,
                RevisionCount = request.RevisionContext is null ? 0 : 1,
                IsFinalized = false,
                FinalizedAt = null,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            };
            _db.ResumeBuilderArtifacts.Add(artifact);
        }
        else
        {
            artifact.TemplateId = template.TemplateId;
            artifact.BuilderSnapshotJson = snapshotJson;
            artifact.GeneratedResumeJson = generatedJson;
            artifact.GenerationModel = modelUsed;
            artifact.LastChangeRequest = request.RevisionContext?.UserChangeRequest;
            artifact.RevisionCount += request.RevisionContext is null ? 0 : 1;
            artifact.IsFinalized = false;
            artifact.FinalizedAt = null;
            artifact.UpdatedAt = now;
            artifact.IsDeleted = false;
        }

        project.CurrentStep = Math.Max(project.CurrentStep, 6);
        project.Status = "in_progress";
        project.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToArtifactResponse(artifact));
    }

    private async Task<(string ModelUsed, ResumeBuilderGeneratedResumeDto? Resume)> ResolveResumePayloadAsync(
        ResumeBuilderTemplateEntity template,
        ResumeBuilderGenerateRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.RevisionContext is null
            && request.PrefilledResumeJson.HasValue
            && request.PrefilledResumeJson.Value.ValueKind is not JsonValueKind.Null
            && request.PrefilledResumeJson.Value.ValueKind is not JsonValueKind.Undefined
            && TryBuildGeneratedResumeFromPrefilled(template, request, request.PrefilledResumeJson.Value, out var prefilledResume))
        {
            _logger.LogInformation("Resume builder generate using prefilled default resume payload. TemplateId={TemplateId}", template.TemplateId);
            return ("default_resume_reuse", prefilledResume);
        }

        _logger.LogInformation("Resume builder generate using Gemini path. TemplateId={TemplateId}", template.TemplateId);
        return await _geminiService.GenerateAsync(template, request, cancellationToken);
    }

    private static bool TryBuildGeneratedResumeFromPrefilled(
        ResumeBuilderTemplateEntity template,
        ResumeBuilderGenerateRequestDto request,
        JsonElement prefilledResumeJson,
        out ResumeBuilderGeneratedResumeDto resume)
    {
        resume = default!;

        ResumeData? prefilledResumeData;
        try
        {
            prefilledResumeData = JsonSerializer.Deserialize<ResumeData>(
                prefilledResumeJson.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return false;
        }

        if (prefilledResumeData?.PersonalInfo is null)
        {
            return false;
        }

        var personal = prefilledResumeData.PersonalInfo;
        var firstTargetJob = prefilledResumeData.TargetJobs?.FirstOrDefault();
        var targetRole = request.TargetRole
            ?? firstTargetJob?.Title
            ?? personal.ProfessionalTitle
            ?? string.Empty;

        var education = (prefilledResumeData.Education ?? new List<EducationEntry>())
            .Select(item => new ResumeBuilderGeneratedEducationItemDto(
                Institution: item.School ?? item.College ?? item.University ?? string.Empty,
                Degree: item.Degree ?? string.Empty,
                FieldOfStudy: item.FieldOfStudy,
                StartYear: item.StartYear,
                EndYear: string.Equals(item.EndYear, "Present", StringComparison.OrdinalIgnoreCase) ? null : item.EndYear,
                IsPresent: string.Equals(item.EndYear, "Present", StringComparison.OrdinalIgnoreCase),
                Marks: item.Marks))
            .ToList();

        var experience = (prefilledResumeData.Experience ?? new List<ExperienceEntry>())
            .Select(item => new ResumeBuilderGeneratedExperienceItemDto(
                Company: item.Company ?? string.Empty,
                Role: item.Role ?? string.Empty,
                StartDate: item.StartDate,
                EndDate: string.Equals(item.EndDate, "Present", StringComparison.OrdinalIgnoreCase) ? null : item.EndDate,
                IsPresent: string.Equals(item.EndDate, "Present", StringComparison.OrdinalIgnoreCase),
                Description: item.Description))
            .ToList();

        var projects = (prefilledResumeData.Projects ?? new List<ProjectEntry>())
            .Select(item => new ResumeBuilderGeneratedProjectItemDto(
                Name: item.Title ?? string.Empty,
                TechStack: item.Technologies ?? string.Empty,
                Description: item.Description))
            .ToList();

        var skills = (prefilledResumeData.Skills ?? new List<string>())
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        resume = new ResumeBuilderGeneratedResumeDto(
            TemplateId: template.TemplateId,
            Profile: new ResumeBuilderGeneratedProfileDto(
                FullName: personal.Name ?? string.Empty,
                ProfessionalRole: personal.ProfessionalTitle ?? string.Empty,
                Email: personal.Email ?? string.Empty,
                Phone: personal.Phone,
                LinkedInUrl: personal.LinkedIn,
                PortfolioUrl: personal.GitHub,
                Location: personal.Location),
            TargetRole: targetRole,
            Summary: personal.Summary ?? firstTargetJob?.Description ?? string.Empty,
            Skills: skills,
            Education: education,
            Experience: experience,
            Projects: projects,
            TemplateStyle: new ResumeBuilderGeneratedTemplateStyleDto(
                TemplateId: template.TemplateId,
                Title: template.Title,
                Description: template.Description,
                StyleGuide: DeserializeJson(string.IsNullOrWhiteSpace(template.StyleGuideJson) ? "{}" : template.StyleGuideJson)),
            GenerationNotes: new List<string> { "Generated directly from resolved default resume data without AI regeneration." },
            RevisionHistory: new List<string>());

        return true;
    }

    private async Task<ResumeBuilderArtifactEntity?> GetLatestArtifactEntityAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return await _db.ResumeBuilderArtifacts
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && !x.IsDeleted, cancellationToken);
    }

    private async Task<ProjectEntity?> GetOwnedProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return null;
        }

        return await _db.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.UserId == userId && !p.IsDeleted, cancellationToken);
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid")
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        return int.TryParse(raw, out userId);
    }

    private static ResumeBuilderArtifactResponseDto ToArtifactResponse(ResumeBuilderArtifactEntity artifact)
    {
        return new ResumeBuilderArtifactResponseDto(
            artifact.Id,
            artifact.ProjectId,
            artifact.TemplateId,
            DeserializeJson(artifact.BuilderSnapshotJson),
            DeserializeJson(artifact.GeneratedResumeJson),
            artifact.GenerationModel,
            artifact.RevisionCount,
            artifact.IsFinalized,
            artifact.FinalizedAt,
            artifact.CreatedAt,
            artifact.UpdatedAt);
    }

    private static JsonElement DeserializeJson(string rawJson)
    {
        return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(rawJson) ? "{}" : rawJson);
    }

    private static T? Deserialize<T>(string rawJson)
    {
        return JsonSerializer.Deserialize<T>(string.IsNullOrWhiteSpace(rawJson) ? "{}" : rawJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static string SanitizeJson<T>(T value)
    {
        return ProjectDatabaseSanitizer.SanitizeJson(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false })) ?? "{}";
    }

    private static string SerializeRenderOptions(JsonElement? renderOptions)
    {
        return ProjectDatabaseSanitizer.SanitizeJson(renderOptions?.GetRawText()) ?? "{}";
    }

    private static string BuildPdfFileName(string? fullName, string templateTitle)
    {
        var safeName = string.IsNullOrWhiteSpace(fullName) ? "resume" : fullName.Trim().ToLowerInvariant().Replace(' ', '-');
        var safeTemplate = string.IsNullOrWhiteSpace(templateTitle) ? "template" : templateTitle.Trim().ToLowerInvariant().Replace(' ', '-');
        return $"{safeName}-{safeTemplate}.pdf";
    }

    private async Task<ResumeBuilderTemplateEntity> ResolveTemplateOrDefaultAsync(string? templateId, CancellationToken cancellationToken)
    {
        var requested = string.IsNullOrWhiteSpace(templateId)
            ? null
            : await _db.ResumeBuilderTemplates.FirstOrDefaultAsync(x => x.TemplateId == templateId && x.IsActive, cancellationToken);

        if (requested is not null)
        {
            return requested;
        }

        var fallback = await _db.ResumeBuilderTemplates
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Title)
            .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);

        if (fallback is null)
        {
            throw new InvalidOperationException("No active resume builder template configured.");
        }

        _logger.LogWarning("Template fallback applied. Requested template: {RequestedTemplateId}, using: {FallbackTemplateId}", templateId, fallback.TemplateId);
        return fallback;
    }
}