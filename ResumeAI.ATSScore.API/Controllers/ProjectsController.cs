using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeAI.ATSScore.API.Data;
using ResumeAI.ATSScore.API.DTO.Projects;
using ResumeAI.ATSScore.API.Models;
using ResumeAI.ATSScore.API.Persistence;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace ResumeAI.ATSScore.API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly ProjectsDbContext _db;
    private readonly ILogger<ProjectsController> _logger;

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

    [HttpGet("history")]
    public async Task<IActionResult> GetProjectHistory([FromQuery] bool includeDeleted = true, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var query = _db.Projects.Where(p => p.UserId == userId);
        if (!includeDeleted)
        {
            query = query.Where(p => !p.IsDeleted);
        }

        var projects = await query
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new
            {
                p.ProjectId,
                p.UserId,
                p.Name,
                p.Type,
                p.Status,
                p.CurrentStep,
                p.CreatedAt,
                p.UpdatedAt,
                p.IsDeleted
            })
            .ToListAsync(cancellationToken);

        return Ok(projects);
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

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreProject([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAnyStateAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var now = DateTime.UtcNow;
        project.IsDeleted = false;
        project.UpdatedAt = now;

        var resumeArtifact = await _db.ResumeArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id, cancellationToken);
        if (resumeArtifact is not null)
        {
            resumeArtifact.IsDeleted = false;
            resumeArtifact.UpdatedAt = now;
        }

        var jdArtifact = await _db.JobDescriptionArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id, cancellationToken);
        if (jdArtifact is not null)
        {
            jdArtifact.IsDeleted = false;
            jdArtifact.UpdatedAt = now;
        }

        var wizardState = await _db.WizardStates.FirstOrDefaultAsync(x => x.ProjectId == id, cancellationToken);
        if (wizardState is not null)
        {
            wizardState.IsDeleted = false;
            wizardState.UpdatedAt = now;
        }

        var atsResults = await _db.AtsResults.Where(x => x.ProjectId == id).ToListAsync(cancellationToken);
        foreach (var result in atsResults)
        {
            result.IsDeleted = false;
        }

        var resumeBuilderArtifact = await _db.ResumeBuilderArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id, cancellationToken);
        if (resumeBuilderArtifact is not null)
        {
            resumeBuilderArtifact.IsDeleted = false;
            resumeBuilderArtifact.UpdatedAt = now;
        }

        var pdfExports = await _db.ResumePdfExports.Where(x => x.ProjectId == id).ToListAsync(cancellationToken);
        foreach (var export in pdfExports)
        {
            export.IsDeleted = false;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { projectId = id, isDeleted = false, updatedAt = now });
    }

    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> PermanentDeleteProject([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var project = await GetOwnedProjectAnyStateAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound(new { message = "Project not found." });
        }

        var resumeArtifact = await _db.ResumeArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id, cancellationToken);
        var jdArtifact = await _db.JobDescriptionArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id, cancellationToken);
        var wizardState = await _db.WizardStates.FirstOrDefaultAsync(x => x.ProjectId == id, cancellationToken);
        var atsResults = await _db.AtsResults.Where(x => x.ProjectId == id).ToListAsync(cancellationToken);
        var resumeBuilderArtifact = await _db.ResumeBuilderArtifacts.FirstOrDefaultAsync(x => x.ProjectId == id, cancellationToken);
        var pdfExports = await _db.ResumePdfExports.Where(x => x.ProjectId == id).ToListAsync(cancellationToken);

        if (pdfExports.Count > 0)
        {
            _db.ResumePdfExports.RemoveRange(pdfExports);
        }

        if (resumeBuilderArtifact is not null)
        {
            _db.ResumeBuilderArtifacts.Remove(resumeBuilderArtifact);
        }

        if (atsResults.Count > 0)
        {
            _db.AtsResults.RemoveRange(atsResults);
        }

        if (wizardState is not null)
        {
            _db.WizardStates.Remove(wizardState);
        }

        if (jdArtifact is not null)
        {
            _db.JobDescriptionArtifacts.Remove(jdArtifact);
        }

        if (resumeArtifact is not null)
        {
            _db.ResumeArtifacts.Remove(resumeArtifact);
        }

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { projectId = id, permanentlyDeleted = true });
    }

    [HttpDelete("account/purge")]
    public async Task<IActionResult> PurgeCurrentUserProjects(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var ownedProjectIds = await _db.Projects
            .Where(p => p.UserId == userId)
            .Select(p => p.ProjectId)
            .ToListAsync(cancellationToken);

        if (ownedProjectIds.Count == 0)
        {
            return Ok(new { userId, deletedProjects = 0 });
        }

        var pdfExports = await _db.ResumePdfExports.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
        var resumeBuilderArtifacts = await _db.ResumeBuilderArtifacts.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
        var atsResults = await _db.AtsResults.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
        var wizardStates = await _db.WizardStates.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
        var jdArtifacts = await _db.JobDescriptionArtifacts.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
        var resumeArtifacts = await _db.ResumeArtifacts.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
        var projects = await _db.Projects.Where(x => x.UserId == userId).ToListAsync(cancellationToken);

        if (pdfExports.Count > 0) _db.ResumePdfExports.RemoveRange(pdfExports);
        if (resumeBuilderArtifacts.Count > 0) _db.ResumeBuilderArtifacts.RemoveRange(resumeBuilderArtifacts);
        if (atsResults.Count > 0) _db.AtsResults.RemoveRange(atsResults);
        if (wizardStates.Count > 0) _db.WizardStates.RemoveRange(wizardStates);
        if (jdArtifacts.Count > 0) _db.JobDescriptionArtifacts.RemoveRange(jdArtifacts);
        if (resumeArtifacts.Count > 0) _db.ResumeArtifacts.RemoveRange(resumeArtifacts);
        if (projects.Count > 0) _db.Projects.RemoveRange(projects);

        var preference = await _db.UserResumePreferences.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (preference is not null)
        {
            _db.UserResumePreferences.Remove(preference);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { userId, deletedProjects = projects.Count });
    }

    [HttpGet("resume-library")]
    public async Task<IActionResult> GetResumeLibrary(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var parserResumes = await _db.ResumeArtifacts
            .Where(x => x.Project.UserId == userId)
            .Select(x => new
            {
                resumeId = $"parser:{x.Id}",
                resumeType = "parser_artifact",
                projectId = x.ProjectId,
                projectName = x.Project.Name,
                sourceType = x.SourceType,
                createdAt = x.CreatedAt,
                updatedAt = x.UpdatedAt,
                isDeleted = x.IsDeleted || x.Project.IsDeleted
            })
            .ToListAsync(cancellationToken);

        var generatedResumes = await _db.ResumeBuilderArtifacts
            .Where(x => x.Project.UserId == userId)
            .Select(x => new
            {
                resumeId = $"builder:{x.Id}",
                resumeType = "resume_builder_artifact",
                projectId = x.ProjectId,
                projectName = x.Project.Name,
                templateId = x.TemplateId,
                createdAt = x.CreatedAt,
                updatedAt = x.UpdatedAt,
                isDeleted = x.IsDeleted || x.Project.IsDeleted
            })
            .ToListAsync(cancellationToken);

        var exportedResumes = await _db.ResumePdfExports
            .Where(x => x.Project.UserId == userId)
            .Select(x => new
            {
                resumeId = $"pdf:{x.Id}",
                resumeType = "resume_pdf_export",
                projectId = x.ProjectId,
                projectName = x.Project.Name,
                templateId = x.TemplateId,
                fileName = x.FileName,
                createdAt = x.CreatedAt,
                updatedAt = x.CreatedAt,
                isDeleted = x.IsDeleted || x.Project.IsDeleted
            })
            .ToListAsync(cancellationToken);

        var preference = await _db.UserResumePreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        return Ok(new
        {
            items = parserResumes.Cast<object>().Concat(generatedResumes.Cast<object>()).Concat(exportedResumes.Cast<object>()),
            defaultResume = preference is null
                ? null
                : new
                {
                    resumeType = preference.DefaultResumeRefType,
                    resumeId = preference.DefaultResumeRefId,
                    updatedAt = preference.UpdatedAt
                }
        });
    }

    [HttpPost("resume-library/default/{resumeId}")]
    public async Task<IActionResult> SetDefaultResume([FromRoute] string resumeId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        if (string.IsNullOrWhiteSpace(resumeId) || !TryParseResumeReference(resumeId, out var refType, out var refGuid))
        {
            return BadRequest(new { message = "Invalid resume reference." });
        }

        var exists = refType switch
        {
            "parser_artifact" => await _db.ResumeArtifacts.AnyAsync(x => x.Id == refGuid && x.Project.UserId == userId, cancellationToken),
            "resume_builder_artifact" => await _db.ResumeBuilderArtifacts.AnyAsync(x => x.Id == refGuid && x.Project.UserId == userId, cancellationToken),
            "resume_pdf_export" => await _db.ResumePdfExports.AnyAsync(x => x.Id == refGuid && x.Project.UserId == userId, cancellationToken),
            _ => false
        };

        if (!exists)
        {
            return NotFound(new { message = "Resume reference not found for this user." });
        }

        var preference = await _db.UserResumePreferences.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = new UserResumePreferenceEntity
            {
                UserId = userId,
                DefaultResumeRefType = refType,
                DefaultResumeRefId = resumeId,
                UpdatedAt = DateTime.UtcNow
            };

            _db.UserResumePreferences.Add(preference);
        }
        else
        {
            preference.DefaultResumeRefType = refType;
            preference.DefaultResumeRefId = resumeId;
            preference.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            resumeType = preference.DefaultResumeRefType,
            resumeId = preference.DefaultResumeRefId,
            updatedAt = preference.UpdatedAt
        });
    }

    [HttpGet("resume-library/default")]
    public async Task<IActionResult> GetDefaultResume(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var preference = await _db.UserResumePreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (preference is null)
        {
            return NotFound(new { message = "Default resume not set." });
        }

        return Ok(new
        {
            resumeType = preference.DefaultResumeRefType,
            resumeId = preference.DefaultResumeRefId,
            updatedAt = preference.UpdatedAt
        });
    }

    [HttpGet("resume-library/default/resolve")]
    public async Task<IActionResult> ResolveDefaultResume([FromQuery] string? module, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var rawModule = (module ?? "ats").Trim().ToLowerInvariant();
        if (rawModule is not ("ats" or "resume_builder"))
        {
            return BadRequest(new { message = "Module must be either 'ats' or 'resume_builder'." });
        }
        var normalizedModule = rawModule;

        var preference = await _db.UserResumePreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (preference is null)
        {
            return NotFound(new { message = "Default resume not set. Configure a default resume in Account Settings first." });
        }

        if (!TryParseResumeReference(preference.DefaultResumeRefId, out var _, out var referenceId))
        {
            return BadRequest(new { message = "Saved default resume reference is invalid. Reconfigure your default resume." });
        }

        var referenceType = preference.DefaultResumeRefType;
        Guid sourceProjectId;
        string sourceType;
        object canonicalParsedResumeJson;

        if (referenceType == "parser_artifact")
        {
            var parserArtifact = await _db.ResumeArtifacts
                .AsNoTracking()
                .Where(x => x.Id == referenceId && x.Project.UserId == userId && !x.IsDeleted && !x.Project.IsDeleted)
                .Select(x => new
                {
                    x.ProjectId,
                    x.SourceType,
                    x.ParsedResumeJson
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (parserArtifact is null)
            {
                return NotFound(new { message = "Saved default resume is stale or deleted. Please set a new default resume." });
            }

            sourceProjectId = parserArtifact.ProjectId;
            sourceType = parserArtifact.SourceType;
            canonicalParsedResumeJson = DeserializeJson(parserArtifact.ParsedResumeJson);
        }
        else if (referenceType == "resume_builder_artifact")
        {
            var builderArtifact = await _db.ResumeBuilderArtifacts
                .AsNoTracking()
                .Where(x => x.Id == referenceId && x.Project.UserId == userId && !x.IsDeleted && !x.Project.IsDeleted)
                .Select(x => new
                {
                    x.ProjectId,
                    x.GeneratedResumeJson,
                    x.BuilderSnapshotJson
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (builderArtifact is null)
            {
                return NotFound(new { message = "Saved default resume is stale or deleted. Please set a new default resume." });
            }

            sourceProjectId = builderArtifact.ProjectId;
            sourceType = "default_resume_builder";

            if (!TryBuildResumeDataFromGeneratedResume(builderArtifact.GeneratedResumeJson, out var mappedResumeData)
                && !TryBuildResumeDataFromBuilderSnapshot(builderArtifact.BuilderSnapshotJson, out mappedResumeData))
            {
                return BadRequest(new { message = "Saved default resume cannot be materialized into canonical JSON. Please choose a different default resume." });
            }

            canonicalParsedResumeJson = mappedResumeData;
        }
        else if (referenceType == "resume_pdf_export")
        {
            var exportArtifact = await _db.ResumePdfExports
                .AsNoTracking()
                .Where(x => x.Id == referenceId && x.Project.UserId == userId && !x.IsDeleted && !x.Project.IsDeleted)
                .Select(x => new
                {
                    x.ProjectId,
                    x.ArtifactId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (exportArtifact is null || exportArtifact.ArtifactId is null)
            {
                return NotFound(new { message = "Saved default resume is stale or deleted. Please set a new default resume." });
            }

            var builderArtifact = await _db.ResumeBuilderArtifacts
                .AsNoTracking()
                .Where(x => x.Id == exportArtifact.ArtifactId.Value && x.Project.UserId == userId && !x.IsDeleted && !x.Project.IsDeleted)
                .Select(x => new
                {
                    x.GeneratedResumeJson,
                    x.BuilderSnapshotJson
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (builderArtifact is null)
            {
                return NotFound(new { message = "Saved default resume source artifact was not found. Please set a new default resume." });
            }

            sourceProjectId = exportArtifact.ProjectId;
            sourceType = "default_resume_pdf_export";

            if (!TryBuildResumeDataFromGeneratedResume(builderArtifact.GeneratedResumeJson, out var mappedResumeData)
                && !TryBuildResumeDataFromBuilderSnapshot(builderArtifact.BuilderSnapshotJson, out mappedResumeData))
            {
                return BadRequest(new { message = "Saved default resume cannot be materialized into canonical JSON. Please choose a different default resume." });
            }

            canonicalParsedResumeJson = mappedResumeData;
        }
        else
        {
            return BadRequest(new { message = "Saved default resume type is unsupported. Please configure your default resume again." });
        }

        return Ok(new
        {
            module = normalizedModule,
            resumeType = referenceType,
            resumeId = preference.DefaultResumeRefId,
            projectId = sourceProjectId,
            sourceType,
            parsedResumeJson = canonicalParsedResumeJson,
            resolvedAt = DateTime.UtcNow
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
        var parsedJson = SanitizeDatabaseJson(request.ParsedResumeJson.GetRawText()) ?? "{}";
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
                wizardState.StateJson = SanitizeDatabaseJson(normalizedWizardState.NormalizedJson) ?? "{}";
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
        var parsedJson = SanitizeDatabaseJson(request.ParsedJdJson.GetRawText()) ?? "{}";
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
            AtsResultJson = SanitizeDatabaseJson(request.AtsResultJson.GetRawText()) ?? "{}",
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
        var requestedStep = Math.Max(1, request.CurrentStep);
        var hasResumeArtifact = await _db.ResumeArtifacts.AsNoTracking().AnyAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        var normalizedState = SetHasResumeArtifactFlag(request.StateJson.GetRawText(), hasResumeArtifact);
        var stateJson = SanitizeDatabaseJson(normalizedState.NormalizedJson) ?? "{}";

        var existing = await _db.WizardStates.FirstOrDefaultAsync(x => x.ProjectId == id && !x.IsDeleted, cancellationToken);
        if (existing is null)
        {
            existing = new WizardStateEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = id,
                Module = normalizedModule,
                CurrentStep = requestedStep,
                StateJson = stateJson,
                UpdatedAt = now,
                IsDeleted = false
            };
            _db.WizardStates.Add(existing);
        }
        else
        {
            existing.Module = normalizedModule;
            existing.CurrentStep = requestedStep;
            existing.StateJson = stateJson;
            existing.UpdatedAt = now;
            existing.IsDeleted = false;
        }

        project.CurrentStep = Math.Max(project.CurrentStep, existing.CurrentStep);
        project.Status = project.CurrentStep >= 4 ? "completed" : "in_progress";
        project.UpdatedAt = now;

        int changedRows;
        try
        {
            changedRows = await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsWizardStateProjectUniqueViolation(ex))
        {
            _logger.LogWarning(ex,
                "Wizard state concurrent insert conflict detected. Retrying as update. ProjectId={ProjectId}, Module={Module}",
                id,
                normalizedModule);

            _db.ChangeTracker.Clear();

            var retryProject = await GetOwnedProjectAsync(id, cancellationToken);
            if (retryProject is null)
            {
                return NotFound(new { message = "Project not found." });
            }

            var retryWizard = await _db.WizardStates.FirstOrDefaultAsync(x => x.ProjectId == id, cancellationToken);
            if (retryWizard is null)
            {
                throw;
            }

            var retryNow = DateTime.UtcNow;
            retryWizard.Module = normalizedModule;
            retryWizard.CurrentStep = requestedStep;
            retryWizard.StateJson = stateJson;
            retryWizard.UpdatedAt = retryNow;
            retryWizard.IsDeleted = false;

            retryProject.CurrentStep = Math.Max(retryProject.CurrentStep, requestedStep);
            retryProject.Status = retryProject.CurrentStep >= 4 ? "completed" : "in_progress";
            retryProject.UpdatedAt = retryNow;

            changedRows = await _db.SaveChangesAsync(cancellationToken);
            existing = retryWizard;
            now = retryNow;
        }

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
            existing.StateJson = SanitizeDatabaseJson(normalizedState.NormalizedJson) ?? "{}";
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

    private async Task<ProjectEntity?> GetOwnedProjectAnyStateAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return null;
        }

        return await _db.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.UserId == userId, cancellationToken);
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
            "default_reuse" => "default_reuse",
            _ => "upload"
        };
    }

    private static bool TryBuildResumeDataFromBuilderSnapshot(string snapshotJson, out ResumeData resumeData)
    {
        resumeData = new ResumeData
        {
            PersonalInfo = new PersonalInfo(),
            Education = new List<EducationEntry>(),
            Experience = new List<ExperienceEntry>(),
            Projects = new List<ProjectEntry>(),
            Skills = new List<string>(),
            TargetJobs = new List<TargetJob>()
        };

        JsonObject root;
        if (!TryParseStateObject(snapshotJson, out root))
        {
            return false;
        }

        var basicInfo = GetObjectPropertyCaseInsensitive(root, "basicInfo");
        if (basicInfo is not null)
        {
            resumeData.PersonalInfo = new PersonalInfo
            {
                Name = ReadString(basicInfo, "fullName"),
                ProfessionalTitle = ReadString(basicInfo, "professionalRole"),
                Email = ReadString(basicInfo, "email"),
                Phone = ReadString(basicInfo, "phone"),
                Location = ReadString(basicInfo, "location"),
                LinkedIn = ReadString(basicInfo, "linkedInUrl"),
                GitHub = ReadString(basicInfo, "portfolioUrl"),
                ExternalLink = BuildExternalLinks(
                    ReadString(basicInfo, "linkedInUrl"),
                    ReadString(basicInfo, "portfolioUrl")),
                Summary = ReadString(basicInfo, "summary")
            };
        }

        var targetJob = GetObjectPropertyCaseInsensitive(root, "targetJob");
        if (targetJob is not null)
        {
            var title = ReadString(targetJob, "customRole");
            if (string.IsNullOrWhiteSpace(title))
            {
                title = ReadString(targetJob, "role");
            }

            var description = ReadString(targetJob, "jobDescriptionText");
            if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(description))
            {
                resumeData.TargetJobs = new List<TargetJob>
                {
                    new()
                    {
                        Title = title,
                        Description = description
                    }
                };
            }
        }

        var educationArray = GetArrayPropertyCaseInsensitive(root, "education");
        if (educationArray is not null)
        {
            foreach (var entryNode in educationArray.OfType<JsonObject>())
            {
                resumeData.Education!.Add(new EducationEntry
                {
                    School = ReadString(entryNode, "institution"),
                    College = ReadString(entryNode, "institution"),
                    University = ReadString(entryNode, "institution"),
                    Degree = ReadString(entryNode, "degree"),
                    FieldOfStudy = ReadString(entryNode, "fieldOfStudy"),
                    StartYear = ReadString(entryNode, "startYear"),
                    EndYear = ReadString(entryNode, "isPresent") == "true" ? "Present" : ReadString(entryNode, "endYear"),
                    Marks = ReadString(entryNode, "marks")
                });
            }
        }

        var experienceArray = GetArrayPropertyCaseInsensitive(root, "experience");
        if (experienceArray is not null)
        {
            foreach (var entryNode in experienceArray.OfType<JsonObject>())
            {
                resumeData.Experience!.Add(new ExperienceEntry
                {
                    Company = ReadString(entryNode, "company"),
                    Role = ReadString(entryNode, "role"),
                    StartDate = ReadString(entryNode, "startDate"),
                    EndDate = ReadString(entryNode, "isPresent") == "true" ? "Present" : ReadString(entryNode, "endDate"),
                    Description = ReadString(entryNode, "description")
                });
            }
        }

        var projectsArray = GetArrayPropertyCaseInsensitive(root, "projects");
        if (projectsArray is not null)
        {
            foreach (var entryNode in projectsArray.OfType<JsonObject>())
            {
                resumeData.Projects!.Add(new ProjectEntry
                {
                    Title = ReadString(entryNode, "name"),
                    Technologies = ReadString(entryNode, "techStack"),
                    Description = ReadString(entryNode, "description")
                });
            }
        }

        return HasUsefulResumeData(resumeData);
    }

    private static bool TryBuildResumeDataFromGeneratedResume(string generatedJson, out ResumeData resumeData)
    {
        resumeData = new ResumeData
        {
            PersonalInfo = new PersonalInfo(),
            Education = new List<EducationEntry>(),
            Experience = new List<ExperienceEntry>(),
            Projects = new List<ProjectEntry>(),
            Skills = new List<string>(),
            TargetJobs = new List<TargetJob>()
        };

        JsonObject root;
        if (!TryParseStateObject(generatedJson, out root))
        {
            return false;
        }

        var profile = GetObjectPropertyCaseInsensitive(root, "profile");
        if (profile is not null)
        {
            var linkedIn = ReadString(profile, "linkedInUrl");
            var portfolio = ReadString(profile, "portfolioUrl");

            resumeData.PersonalInfo = new PersonalInfo
            {
                Name = ReadString(profile, "fullName"),
                ProfessionalTitle = ReadString(profile, "professionalRole"),
                Email = ReadString(profile, "email"),
                Phone = ReadString(profile, "phone"),
                Location = ReadString(profile, "location"),
                LinkedIn = linkedIn,
                GitHub = portfolio,
                ExternalLink = BuildExternalLinks(linkedIn, portfolio),
                Summary = ReadString(root, "summary")
            };
        }

        var targetRole = ReadString(root, "targetRole");
        var targetDescription = ReadString(root, "summary");
        if (!string.IsNullOrWhiteSpace(targetRole) || !string.IsNullOrWhiteSpace(targetDescription))
        {
            resumeData.TargetJobs = new List<TargetJob>
            {
                new()
                {
                    Title = targetRole,
                    Description = targetDescription
                }
            };
        }

        var skills = GetArrayPropertyCaseInsensitive(root, "skills");
        if (skills is not null)
        {
            resumeData.Skills = skills
                .OfType<JsonNode>()
                .Select(node => node?.ToString()?.Trim())
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var educationArray = GetArrayPropertyCaseInsensitive(root, "education");
        if (educationArray is not null)
        {
            foreach (var entryNode in educationArray.OfType<JsonObject>())
            {
                resumeData.Education!.Add(new EducationEntry
                {
                    School = ReadString(entryNode, "institution"),
                    College = ReadString(entryNode, "institution"),
                    University = ReadString(entryNode, "institution"),
                    Degree = ReadString(entryNode, "degree"),
                    FieldOfStudy = ReadString(entryNode, "fieldOfStudy"),
                    StartYear = ReadString(entryNode, "startYear"),
                    EndYear = ReadString(entryNode, "isPresent") == "true" ? "Present" : ReadString(entryNode, "endYear"),
                    Marks = ReadString(entryNode, "marks")
                });
            }
        }

        var experienceArray = GetArrayPropertyCaseInsensitive(root, "experience");
        if (experienceArray is not null)
        {
            foreach (var entryNode in experienceArray.OfType<JsonObject>())
            {
                resumeData.Experience!.Add(new ExperienceEntry
                {
                    Company = ReadString(entryNode, "company"),
                    Role = ReadString(entryNode, "role"),
                    StartDate = ReadString(entryNode, "startDate"),
                    EndDate = ReadString(entryNode, "isPresent") == "true" ? "Present" : ReadString(entryNode, "endDate"),
                    Description = ReadString(entryNode, "description")
                });
            }
        }

        var projectsArray = GetArrayPropertyCaseInsensitive(root, "projects");
        if (projectsArray is not null)
        {
            foreach (var entryNode in projectsArray.OfType<JsonObject>())
            {
                resumeData.Projects!.Add(new ProjectEntry
                {
                    Title = ReadString(entryNode, "name"),
                    Technologies = ReadString(entryNode, "techStack"),
                    Description = ReadString(entryNode, "description")
                });
            }
        }

        return HasUsefulResumeData(resumeData);
    }

    private static bool HasUsefulResumeData(ResumeData resumeData)
    {
        if (resumeData.PersonalInfo is not null)
        {
            if (!string.IsNullOrWhiteSpace(resumeData.PersonalInfo.Name)
                || !string.IsNullOrWhiteSpace(resumeData.PersonalInfo.ProfessionalTitle)
                || !string.IsNullOrWhiteSpace(resumeData.PersonalInfo.Email)
                || !string.IsNullOrWhiteSpace(resumeData.PersonalInfo.Summary))
            {
                return true;
            }
        }

        var hasEducation = (resumeData.Education ?? new List<EducationEntry>()).Any(item =>
            !string.IsNullOrWhiteSpace(item.School)
            || !string.IsNullOrWhiteSpace(item.College)
            || !string.IsNullOrWhiteSpace(item.University)
            || !string.IsNullOrWhiteSpace(item.Degree)
            || !string.IsNullOrWhiteSpace(item.FieldOfStudy));

        if (hasEducation)
        {
            return true;
        }

        var hasExperience = (resumeData.Experience ?? new List<ExperienceEntry>()).Any(item =>
            !string.IsNullOrWhiteSpace(item.Company)
            || !string.IsNullOrWhiteSpace(item.Role)
            || !string.IsNullOrWhiteSpace(item.Description));

        if (hasExperience)
        {
            return true;
        }

        var hasProjects = (resumeData.Projects ?? new List<ProjectEntry>()).Any(item =>
            !string.IsNullOrWhiteSpace(item.Title)
            || !string.IsNullOrWhiteSpace(item.Technologies)
            || !string.IsNullOrWhiteSpace(item.Description));

        if (hasProjects)
        {
            return true;
        }

        return (resumeData.Skills ?? new List<string>()).Any(skill => !string.IsNullOrWhiteSpace(skill));
    }

    private static string? ReadString(JsonObject obj, string key)
    {
        if (!TryGetPropertyValueCaseInsensitive(obj, key, out var node) || node is null)
        {
            return null;
        }

        try
        {
            if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var stringValue))
                {
                    return stringValue;
                }

                if (value.TryGetValue<bool>(out var boolValue))
                {
                    return boolValue ? "true" : "false";
                }
            }
        }
        catch
        {
        }

        return node.ToJsonString().Trim('"');
    }

    private static JsonObject? GetObjectPropertyCaseInsensitive(JsonObject obj, string key)
    {
        return TryGetPropertyValueCaseInsensitive(obj, key, out var node)
            ? node as JsonObject
            : null;
    }

    private static JsonArray? GetArrayPropertyCaseInsensitive(JsonObject obj, string key)
    {
        return TryGetPropertyValueCaseInsensitive(obj, key, out var node)
            ? node as JsonArray
            : null;
    }

    private static bool TryGetPropertyValueCaseInsensitive(JsonObject obj, string key, out JsonNode? node)
    {
        if (obj.TryGetPropertyValue(key, out node))
        {
            return true;
        }

        foreach (var pair in obj)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                node = pair.Value;
                return true;
            }
        }

        node = null;
        return false;
    }

    private static List<string> BuildExternalLinks(string? linkedIn, string? portfolio)
    {
        var links = new List<string>();
        if (!string.IsNullOrWhiteSpace(linkedIn))
        {
            links.Add(linkedIn);
        }

        if (!string.IsNullOrWhiteSpace(portfolio))
        {
            links.Add(portfolio);
        }

        return links;
    }

    private static string? SanitizeDatabaseText(string? input)
    {
        return ProjectDatabaseSanitizer.SanitizeText(input);
    }

    private static string? SanitizeDatabaseJson(string? input)
    {
        return ProjectDatabaseSanitizer.SanitizeJson(input);
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

    private static bool IsWizardStateProjectUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pg
            && pg.SqlState == "23505"
            && string.Equals(pg.ConstraintName, "IX_wizard_state_project_id", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseResumeReference(string resumeId, out string referenceType, out Guid referenceId)
    {
        referenceType = string.Empty;
        referenceId = Guid.Empty;

        var parts = resumeId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!Guid.TryParse(parts[1], out referenceId))
        {
            return false;
        }

        referenceType = parts[0].ToLowerInvariant() switch
        {
            "parser" => "parser_artifact",
            "builder" => "resume_builder_artifact",
            "pdf" => "resume_pdf_export",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(referenceType);
    }
}
