using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using ResumeAI.ATSScore.API.DTO;
using ResumeAI.ATSScore.API.Interfaces;
using ResumeAI.ATSScore.API.Data;
using ResumeAI.ATSScore.API.Persistence;

namespace ResumeAI.ATSScore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
/// <summary>
/// ATS scoring endpoint for evaluating resumes against job descriptions.
/// </summary>
public class AtsController : ControllerBase
{
    private readonly IAtsScoringService _atsScoringService;
    private readonly ILogger<AtsController> _logger;
    private readonly ProjectsDbContext? _projectsDb;

    public AtsController(
        IAtsScoringService atsScoringService,
        ILogger<AtsController> logger,
        ProjectsDbContext? projectsDb = null)
    {
        _atsScoringService = atsScoringService;
        _logger = logger;
        _projectsDb = projectsDb;
    }

    /// <summary>
    /// Score a resume against a job description.
    /// </summary>
    [HttpPost("score")]
    public async Task<IActionResult> ScoreResume(
        [FromBody] AtsScoreRequestDto request,
        CancellationToken cancellationToken
    )
    {
        if (request == null)
        {
            return BadRequest(new { message = "Request body cannot be empty" });
        }

        if (request.ResumeData == null)
        {
            return BadRequest(new { message = "Resume data is required" });
        }

        if (string.IsNullOrWhiteSpace(request.JobDescriptionText))
        {
            return BadRequest(new { message = "Job description text is required" });
        }

        if (string.IsNullOrWhiteSpace(request.JobRole))
        {
            return BadRequest(new { message = "Job role is required" });
        }

        try
        {
            Guid? projectGuid = null;
            int? currentUserId = null;
            ProjectEntity? ownedProject = null;

            if (!string.IsNullOrWhiteSpace(request.ProjectId))
            {
                if (!Guid.TryParse(request.ProjectId, out var parsedProjectId))
                {
                    return BadRequest(new { message = "ProjectId must be a valid UUID." });
                }

                projectGuid = parsedProjectId;

                if (!TryGetCurrentUserId(out var resolvedUserId))
                {
                    return Unauthorized(new { message = "Authenticated user id was not found in token." });
                }

                currentUserId = resolvedUserId;

                if (_projectsDb != null)
                {
                    ownedProject = await _projectsDb.Projects
                        .FirstOrDefaultAsync(p => p.ProjectId == parsedProjectId && p.UserId == resolvedUserId && !p.IsDeleted, cancellationToken);

                    if (ownedProject == null)
                    {
                        return NotFound(new { message = "Project not found for current user." });
                    }

                    var latestResult = await _projectsDb.AtsResults
                        .Where(r => r.ProjectId == parsedProjectId && !r.IsDeleted)
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (latestResult != null)
                    {
                        try
                        {
                            var cachedResponse = JsonSerializer.Deserialize<AtsScoreResponseDto>(
                                latestResult.AtsResultJson,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            if (cachedResponse != null)
                            {
                                _logger.LogInformation("Returning cached ATS score result for project {ProjectId}", parsedProjectId);
                                return Ok(cachedResponse);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize cached ATS result for project {ProjectId}. Recomputing.", parsedProjectId);
                        }
                    }
                }
            }

            _logger.LogInformation(
                "ATS scoring request received for role: {JobRole}, Project: {ProjectId}",
                request.JobRole,
                request.ProjectId ?? "N/A"
            );

            var result = await _atsScoringService.ScoreResumeAsync(
                request.ResumeData,
                request.JobDescriptionText,
                request.JobRole,
                request.CustomRole,
                cancellationToken
            );

            if (projectGuid.HasValue && _projectsDb != null && currentUserId.HasValue && ownedProject != null)
            {
                try
                {
                    var serialized = ProjectDatabaseSanitizer.SanitizeJson(JsonSerializer.Serialize(result)) ?? "{}";
                    var persisted = new AtsResultEntity
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectGuid.Value,
                        JobRole = request.JobRole,
                        CustomRole = request.CustomRole,
                        AtsResultJson = serialized,
                        OverallScore = result.OverallScore,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    _projectsDb.AtsResults.Add(persisted);

                    ownedProject.CurrentStep = Math.Max(ownedProject.CurrentStep, 4);
                    ownedProject.Status = "completed";
                    ownedProject.UpdatedAt = DateTime.UtcNow;

                    await _projectsDb.SaveChangesAsync(cancellationToken);

                    Console.WriteLine($"[AtsController] ATS insights persisted. ProjectId={projectGuid.Value}, AtsResultId={persisted.Id}, CreatedAtUtc={persisted.CreatedAt:O}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ATS result persistence failed for project {ProjectId}. Returning computed response.", projectGuid.Value);
                }
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument in ATS scoring: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ATS scoring request was cancelled");
            return StatusCode(StatusCodes.Status408RequestTimeout, new { message = "Request timeout" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ATS scoring");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while scoring the resume. Please try again." }
            );
        }
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            service = "ResumeAI.ATSScore.API",
            utcNow = DateTime.UtcNow
        });
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid")
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        return int.TryParse(raw, out userId);
    }
}
