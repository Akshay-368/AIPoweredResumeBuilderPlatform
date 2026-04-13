using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeAI.ATSScore.API.Data;
using ResumeAI.ATSScore.API.DTO.Projects;
using ResumeAI.ATSScore.API.Persistence;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ResumeAI.ATSScore.API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly ProjectsDbContext _db;
    private readonly ILogger<ProjectsController> _logger;
    private static readonly Encoding DbEncoding = CreateDbSafeEncoding();

    public ProjectsController(ProjectsDbContext db, ILogger<ProjectsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var now = DateTime.UtcNow;
        var project = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Type = NormalizeType(request.Type),
            Status = NormalizeStatus(request.Status),
            CurrentStep = request.CurrentStep.HasValue ? Math.Max(1, request.CurrentStep.Value) : 1,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToProjectResponse(project));
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var projects = await _db.Projects
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(cancellationToken);

        return Ok(projects.Select(ToProjectResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProject([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        return Ok(ToProjectResponse(project));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateProject([FromRoute] Guid id, [FromBody] UpdateProjectRequestDto request, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            project.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            project.Type = NormalizeType(request.Type);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            project.Status = NormalizeStatus(request.Status);
        }

        if (request.CurrentStep.HasValue)
        {
            project.CurrentStep = Math.Max(1, request.CurrentStep.Value);
        }

        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToProjectResponse(project));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProject([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var now = DateTime.UtcNow;

        project.IsDeleted = true;
        project.UpdatedAt = now;

        var jdArtifact = await _db.JobDescriptionArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        if (jdArtifact is not null)
        {
            jdArtifact.IsDeleted = true;
            jdArtifact.UpdatedAt = now;
        }

        var wizardState = await _db.WizardStates.FirstOrDefaultAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        if (wizardState is not null)
        {
            wizardState.IsDeleted = true;
            wizardState.UpdatedAt = now;
        }

        var atsResults = await _db.AtsResults.Where(x => x.ProjectId == id && !x.IsDeleted).ToListAsync(cancellationToken);
        foreach (var atsResult in atsResults)
        {
            atsResult.IsDeleted = true;
        }

        var changedRows = await _db.SaveChangesAsync(cancellationToken);

        Console.WriteLine($"[ProjectsController] Project soft-delete succeeded. ProjectId={id}, ChangedRows={changedRows}, JdDeleted={(jdArtifact is not null)}, WizardDeleted={(wizardState is not null)}, AtsDeletedCount={atsResults.Count}, ResumeRetained=true, UpdatedAtUtc={now:O}");

        return Ok(new
        {
            projectId = id,
            isDeleted = true,
            jdDeleted = jdArtifact is not null,
            wizardDeleted = wizardState is not null,
            atsDeletedCount = atsResults.Count,
            resumeRetained = true,
            updatedAt = now
        });
    }

    [HttpPut("{id:guid}/resume-artifact")]
    public async Task<IActionResult> UpsertResumeArtifact([FromRoute] Guid id, [FromBody] UpsertResumeArtifactRequestDto request, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var now = DateTime.UtcNow;
        var parsedJson = SanitizeDatabaseText(request.ParsedResumeJson.GetRawText()) ?? "{}";
        var sanitizedRawText = SanitizeDatabaseText(request.RawText);

        var isNewInsert = false;
        var existing = await _db.ResumeArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id, cancellationToken);
        if (existing is null)
        {
            isNewInsert = true;
            existing = new ResumeArtifactEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = id,
                RawText = sanitizedRawText,
                ParsedResumeJson = parsedJson,
                SourceType = NormalizeSourceType(request.SourceType),
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            };
            _db.ResumeArtifacts.Add(existing);
        }
        else
        {
            existing.RawText = sanitizedRawText;
            existing.ParsedResumeJson = parsedJson;
            existing.SourceType = NormalizeSourceType(request.SourceType);
            existing.UpdatedAt = now;
            existing.IsDeleted = false;
        }

        var wizardState = await _db.WizardStates.FirstOrDefaultAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        if (wizardState is not null)
        {
            var normalizedWizardState = SetHasResumeArtifactFlag(wizardState.StateJson, hasResumeArtifact: true);
            if (normalizedWizardState.WasChanged)
            {
                wizardState.StateJson = SanitizeDatabaseText(normalizedWizardState.NormalizedJson) ?? "{}";
                wizardState.UpdatedAt = now;
            }
        }

        project.CurrentStep = Math.Max(project.CurrentStep, 2);
        project.Status = project.CurrentStep >= 4 ? "completed" : "in_progress";
        project.UpdatedAt = now;

        int changedRows;
        try
        {
            changedRows = await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Resume artifact {Operation} failed. ProjectId={ProjectId}, ArtifactId={ArtifactId}, RawTextLength={RawTextLength}, SourceType={SourceType}",
                isNewInsert ? "insert" : "update",
                id,
                existing.Id,
                sanitizedRawText?.Length ?? 0,
                existing.SourceType);

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Resume artifact persistence failed due to a database write error.",
                details = ex.GetBaseException().Message
            });
        }

    Console.WriteLine($"[ProjectsController] Resume artifact {(isNewInsert ? "insert" : "update")} succeeded. ProjectId={id}, ArtifactId={existing.Id}, ChangedRows={changedRows}, UpdatedAtUtc={now:O}");

        return Ok(new
        {
            projectId = id,
            rawText = existing.RawText,
            parsedResumeJson = DeserializeJson(existing.ParsedResumeJson),
            sourceType = existing.SourceType,
            updatedAt = existing.UpdatedAt
        });
    }

    [HttpGet("{id:guid}/resume-artifact")]
    public async Task<IActionResult> GetResumeArtifact([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var existing = await _db.ResumeArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        if (existing is null)
        {
            return NotFound(new { message = "Resume artifact not found." });
        }

        return Ok(new
        {
            projectId = id,
            rawText = existing.RawText,
            parsedResumeJson = DeserializeJson(existing.ParsedResumeJson),
            sourceType = existing.SourceType,
            updatedAt = existing.UpdatedAt
        });
    }

    [HttpPut("{id:guid}/jd-artifact")]
    public async Task<IActionResult> UpsertJdArtifact([FromRoute] Guid id, [FromBody] UpsertJdArtifactRequestDto request, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var now = DateTime.UtcNow;
        var parsedJson = SanitizeDatabaseText(request.ParsedJdJson.GetRawText()) ?? "{}";
        var sanitizedRawText = SanitizeDatabaseText(request.RawText);

        var existing = await _db.JobDescriptionArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        if (existing is null)
        {
            existing = new JobDescriptionArtifactEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = id,
            RawText = sanitizedRawText,
                ParsedJdJson = parsedJson,
                SourceType = NormalizeSourceType(request.SourceType),
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            };
            _db.JobDescriptionArtifacts.Add(existing);
        }
        else
        {
            existing.RawText = sanitizedRawText;
            existing.ParsedJdJson = parsedJson;
            existing.SourceType = NormalizeSourceType(request.SourceType);
            existing.UpdatedAt = now;
            existing.IsDeleted = false;
        }

        project.CurrentStep = Math.Max(project.CurrentStep, 3);
        project.Status = project.CurrentStep >= 4 ? "completed" : "in_progress";
        project.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            projectId = id,
            rawText = existing.RawText,
            parsedJdJson = DeserializeJson(existing.ParsedJdJson),
            sourceType = existing.SourceType,
            updatedAt = existing.UpdatedAt
        });
    }

    [HttpGet("{id:guid}/jd-artifact")]
    public async Task<IActionResult> GetJdArtifact([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var existing = await _db.JobDescriptionArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        if (existing is null)
        {
            return NotFound(new { message = "JD artifact not found." });
        }

        return Ok(new
        {
            projectId = id,
            rawText = existing.RawText,
            parsedJdJson = DeserializeJson(existing.ParsedJdJson),
            sourceType = existing.SourceType,
            updatedAt = existing.UpdatedAt
        });
    }

    [HttpPost("{id:guid}/ats-results")]
    public async Task<IActionResult> AddAtsResult([FromRoute] Guid id, [FromBody] UpsertAtsResultRequestDto request, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var row = new AtsResultEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = id,
            JobRole = request.JobRole,
            CustomRole = request.CustomRole,
            AtsResultJson = request.AtsResultJson.GetRawText(),
            OverallScore = request.OverallScore,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.AtsResults.Add(row);

        project.CurrentStep = Math.Max(project.CurrentStep, 4);
        project.Status = "completed";
        project.UpdatedAt = DateTime.UtcNow;

        var changedRows = await _db.SaveChangesAsync(cancellationToken);

        Console.WriteLine($"[ProjectsController] ATS insights insert succeeded. ProjectId={id}, AtsResultId={row.Id}, ChangedRows={changedRows}, CreatedAtUtc={row.CreatedAt:O}");

        return Ok(new
        {
            id = row.Id,
            projectId = id,
            jobRole = row.JobRole,
            customRole = row.CustomRole,
            overallScore = row.OverallScore,
            createdAt = row.CreatedAt
        });
    }

    [HttpGet("{id:guid}/ats-results/latest")]
    public async Task<IActionResult> GetLatestAtsResult([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var latest = await _db.AtsResults
            .Where(r => r.ProjectId == id && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return NotFound(new { message = "ATS result not found." });
        }

        return Ok(new
        {
            id = latest.Id,
            projectId = id,
            jobRole = latest.JobRole,
            customRole = latest.CustomRole,
            overallScore = latest.OverallScore,
            createdAt = latest.CreatedAt,
            atsResultJson = DeserializeJson(latest.AtsResultJson)
        });
    }

    [HttpPut("{id:guid}/wizard-state/{module}")]
    public async Task<IActionResult> UpsertWizardState([FromRoute] Guid id, [FromRoute] string module, [FromBody] UpsertWizardStateRequestDto request, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var normalizedModule = NormalizeModule(module);
        var now = DateTime.UtcNow;
        var hasResumeArtifact = await _db.ResumeArtifacts.AsNoTracking().AnyAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        var normalizedState = SetHasResumeArtifactFlag(request.StateJson.GetRawText(), hasResumeArtifact);
        var stateJson = SanitizeDatabaseText(normalizedState.NormalizedJson) ?? "{}";

        var existing = await _db.WizardStates.FirstOrDefaultAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        if (existing is null)
        {
            existing = new WizardStateEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = id,
                Module = normalizedModule,
                CurrentStep = Math.Max(1, request.CurrentStep),
                StateJson = stateJson,
                UpdatedAt = now,
                IsDeleted = false
            };
            _db.WizardStates.Add(existing);
        }
        else
        {
            existing.Module = normalizedModule;
            existing.CurrentStep = Math.Max(1, request.CurrentStep);
            existing.StateJson = stateJson;
            existing.UpdatedAt = now;
            existing.IsDeleted = false;
        }

        project.CurrentStep = Math.Max(project.CurrentStep, existing.CurrentStep);
        project.Status = project.CurrentStep >= 4 ? "completed" : "in_progress";
        project.UpdatedAt = now;

        var changedRows = await _db.SaveChangesAsync(cancellationToken);

        Console.WriteLine($"[ProjectsController] Wizard state upsert succeeded. ProjectId={id}, Module={existing.Module}, HasResumeArtifact={hasResumeArtifact}, ChangedRows={changedRows}, UpdatedAtUtc={now:O}");

        return Ok(new
        {
            projectId = id,
            module = existing.Module,
            currentStep = existing.CurrentStep,
            stateJson = DeserializeJson(existing.StateJson),
            updatedAt = existing.UpdatedAt
        });
    }

    [HttpGet("{id:guid}/wizard-state/{module}")]
    public async Task<IActionResult> GetWizardState([FromRoute] Guid id, [FromRoute] string module, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var normalizedModule = NormalizeModule(module);
        var existing = await _db.WizardStates
            .FirstOrDefaultAsync(x => x.ProjectId == id && x.Module == normalizedModule && !x.IsDeleted, cancellationToken);

        if (existing is null)
        {
            return NotFound(new { message = "Wizard state not found." });
        }

        var hasResumeArtifact = await _db.ResumeArtifacts.AsNoTracking().AnyAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        var normalizedState = SetHasResumeArtifactFlag(existing.StateJson, hasResumeArtifact);
        if (normalizedState.WasChanged)
        {
            existing.StateJson = SanitizeDatabaseText(normalizedState.NormalizedJson) ?? "{}";
            existing.UpdatedAt = DateTime.UtcNow;

            var changedRows = await _db.SaveChangesAsync(cancellationToken);
            Console.WriteLine($"[ProjectsController] Wizard state reconciled on read. ProjectId={id}, Module={existing.Module}, HasResumeArtifact={hasResumeArtifact}, ChangedRows={changedRows}, UpdatedAtUtc={existing.UpdatedAt:O}");
        }

        return Ok(new
        {
            projectId = id,
            module = existing.Module,
            currentStep = existing.CurrentStep,
            stateJson = DeserializeJson(normalizedState.NormalizedJson),
            updatedAt = existing.UpdatedAt
        });
    }

    private async Task<ProjectEntity?> GetOwnedProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return null;
        }

        return await _db.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.UserId == userId && !p.IsDeleted, cancellationToken);

    }

    private static ProjectResponseDto ToProjectResponse(ProjectEntity project)
    {
        return new ProjectResponseDto(
            project.ProjectId,
            project.UserId,
            project.Name,
            project.Type,
            project.Status,
            project.CurrentStep,
            project.CreatedAt,
            project.UpdatedAt);
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid")
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        return int.TryParse(raw, out userId);
    }

    private static string NormalizeType(string? type)
    {
        var normalized = (type ?? "ATS").Trim();
        return normalized switch
        {
            "ATS" => "ATS",
            "Resume" => "Resume",
            "Template" => "Template",
            _ => "ATS"
        };
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = (status ?? "draft").Trim().ToLowerInvariant();
        return normalized switch
        {
            "draft" => "draft",
            "in_progress" => "in_progress",
            "completed" => "completed",
            _ => "draft"
        };
    }

    private static string NormalizeModule(string module)
    {
        var normalized = module.Trim().ToLowerInvariant();
        return normalized switch
        {
            "ats" => "ats",
            "resume_builder" => "resume_builder",
            _ => "ats"
        };
    }

    private static string NormalizeSourceType(string? sourceType)
    {
        var normalized = (sourceType ?? "upload").Trim().ToLowerInvariant();
        return normalized switch
        {
            "upload" => "upload",
            "paste" => "paste",
            _ => "upload"
        };
    }

    private static string? SanitizeDatabaseText(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // PostgreSQL text/json values cannot contain NUL (\u0000), and this DB
        // uses WIN1252 server encoding, so remove characters outside that codepage.
        var withoutNulls = input.Replace("\0", string.Empty);
        var encoded = DbEncoding.GetBytes(withoutNulls);
        return DbEncoding.GetString(encoded);
    }

    private static Encoding CreateDbSafeEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            1252,
            new EncoderReplacementFallback(string.Empty),
            new DecoderReplacementFallback(string.Empty));
    }

    private static object DeserializeJson(string rawJson)
    {
        try
        {
            return JsonSerializer.Deserialize<object>(rawJson) ?? new { };
        }
        catch
        {
            return new { };
        }
    }

    private static (string NormalizedJson, bool WasChanged) SetHasResumeArtifactFlag(string rawStateJson, bool hasResumeArtifact)
    {
        var parsedObject = TryParseStateObject(rawStateJson, out var stateObject);
        var existingFlag = TryReadBooleanProperty(stateObject, "hasResumeArtifact");

        stateObject["hasResumeArtifact"] = hasResumeArtifact;

        var normalizedJson = stateObject.ToJsonString();
        var wasChanged = !parsedObject || existingFlag != hasResumeArtifact;
        return (normalizedJson, wasChanged);
    }

    private static bool TryParseStateObject(string rawStateJson, out JsonObject stateObject)
    {
        try
        {
            var node = JsonNode.Parse(rawStateJson);
            if (node is JsonObject jsonObject)
            {
                stateObject = jsonObject;
                return true;
            }
        }
        catch
        {
        }

        stateObject = new JsonObject();
        return false;
    }

    private static bool? TryReadBooleanProperty(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<bool>();
        }
        catch
        {
            return null;
        }
    }
}
