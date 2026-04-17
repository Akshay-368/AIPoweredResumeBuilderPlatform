using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResumeAI.FileParserToJson.DTO;
using ResumeAI.FileParserToJson.Interfaces;
using ResumeAI.FileParserToJson.Services;
using System.Text.Json;

namespace ResumeAI.FileParserToJson.Controllers;

[ApiController]
[Route("api/parser")]
[Authorize(Policy = "CanParseResume")]
public class ParserController : ControllerBase
{
    private readonly IResumeParserService _parser;
    private readonly IAiResumeParser _aiParser; // Use the orchestrator instead of direct Gemini service
    private readonly IAiJobDescriptionParser _aiJdParser; // Job description parser orchestrator
    private readonly ProjectsPersistenceClient _projectsPersistence;
    private readonly ILogger<ParserController> _logger;

    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB limit for uploaded files
    private static bool IsFileSizeTooLarge(IFormFile file) => file.Length > MaxFileSizeBytes;
    public ParserController(
        IResumeParserService parser,
        IAiResumeParser aiParser,
        IAiJobDescriptionParser aiJdParser,
        ProjectsPersistenceClient projectsPersistence,
        ILogger<ParserController> logger)
    {
        _parser = parser;
        _aiParser = aiParser;
        _aiJdParser = aiJdParser;
        _projectsPersistence = projectsPersistence;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadResume(IFormFile file, [FromQuery] string? projectId, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded or file is empty." });
        }

        if (IsFileSizeTooLarge(file))
        {
            return BadRequest(new { message = $"File size exceeds the allowed limit of {MaxFileSizeBytes / (1024 * 1024) } megabytes." });
        }

        try 
        {
            var bearerToken = GetBearerToken();
            if (TryGetProjectId(projectId, out var parsedProjectId))
            {
                var cached = await _projectsPersistence.TryGetResumeArtifactAsync(parsedProjectId, bearerToken, cancellationToken);
                if (cached is not null)
                {
                    return Ok(cached);
                }
            }

            // 1. Extract and Validate
            string rawText = _parser.ParseFile(file);

            // 2. AI Structuring
            var structuredData = await _aiParser.ParseAsync(rawText, cancellationToken);

            if (structuredData is not null)
            {
                var formattedJson = JsonSerializer.Serialize(structuredData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                _logger.LogInformation("Parsed resume JSON output:\n{ParsedJson}", formattedJson);
            }
            else
            {
                _logger.LogWarning("AI parser returned null structured JSON output for file {FileName}.", file.FileName);
            }

            if (structuredData is not null && TryGetProjectId(projectId, out var resumeProjectId))
            {
                var resumePersistResult = await _projectsPersistence.UpsertResumeArtifactWithDetailsAsync(
                    resumeProjectId,
                    rawText,
                    structuredData,
                    "upload",
                    bearerToken,
                    cancellationToken);

                var resumePersisted = resumePersistResult.Success;

                var wizardPersisted = await _projectsPersistence.UpsertWizardStateAsync(
                    resumeProjectId,
                    "ats",
                    2,
                    new
                    {
                        resumeFileName = file.FileName,
                        hasResumeArtifact = resumePersisted
                    },
                    bearerToken,
                    cancellationToken);

                Console.WriteLine($"[ParserController] Resume upload persistence completed. ProjectId={resumeProjectId}, ResumeSaved={resumePersisted}, WizardSaved={wizardPersisted}, HasResumeArtifact={resumePersisted}");

                if (!resumePersisted)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        message = "Resume parsed, but persisting parsed resume JSON failed. Please retry.",
                        persistenceStatusCode = resumePersistResult.StatusCode,
                        persistenceError = string.IsNullOrWhiteSpace(resumePersistResult.ErrorBody)
                            ? null
                            : resumePersistResult.ErrorBody
                    });
                }
            }

            return Ok(structuredData);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("upload-text")]
    public async Task<IActionResult> UploadResumeText([FromBody] RawTextRequest request, [FromQuery] string? projectId, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RawText))
        {
            return BadRequest(new { message = "Resume text is required." });
        }

        try
        {
            var bearerToken = GetBearerToken();
            if (TryGetProjectId(projectId, out var parsedProjectId))
            {
                var cached = await _projectsPersistence.TryGetResumeArtifactAsync(parsedProjectId, bearerToken, cancellationToken);
                if (cached is not null)
                {
                    return Ok(cached);
                }
            }

            var structuredData = await _aiParser.ParseAsync(request.RawText, cancellationToken);

            if (structuredData is not null)
            {
                var formattedJson = JsonSerializer.Serialize(structuredData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                _logger.LogInformation("Parsed resume text JSON output:\n{ParsedJson}", formattedJson);
            }

            if (structuredData is not null && TryGetProjectId(projectId, out var resumeProjectId))
            {
                var resumePersistResult = await _projectsPersistence.UpsertResumeArtifactWithDetailsAsync(
                    resumeProjectId,
                    request.RawText,
                    structuredData,
                    "paste",
                    bearerToken,
                    cancellationToken);

                var resumePersisted = resumePersistResult.Success;

                var wizardPersisted = await _projectsPersistence.UpsertWizardStateAsync(
                    resumeProjectId,
                    "ats",
                    2,
                    new
                    {
                        hasResumeArtifact = resumePersisted,
                        sourceType = "paste"
                    },
                    bearerToken,
                    cancellationToken);

                Console.WriteLine($"[ParserController] Resume text persistence completed. ProjectId={resumeProjectId}, ResumeSaved={resumePersisted}, WizardSaved={wizardPersisted}, HasResumeArtifact={resumePersisted}");

                if (!resumePersisted)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        message = "Resume parsed, but persisting parsed resume JSON failed. Please retry.",
                        persistenceStatusCode = resumePersistResult.StatusCode,
                        persistenceError = string.IsNullOrWhiteSpace(resumePersistResult.ErrorBody)
                            ? null
                            : resumePersistResult.ErrorBody
                    });
                }
            }

            return Ok(structuredData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing pasted resume text.");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Parse a job description file (PDF or DOCX) into structured JSON.
    /// </summary>
    [HttpPost("job-description")]
    public async Task<IActionResult> ParseJobDescription(IFormFile file, [FromQuery] string? projectId, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded or file is empty." });
        }

        if (IsFileSizeTooLarge(file))
        {
            return BadRequest (new
            {
                message = $"File size exceeds the allowed limit of {MaxFileSizeBytes / (1024 * 1024)} megabytes.",
            });
        }

        try
        {
            var bearerToken = GetBearerToken();
            if (TryGetProjectId(projectId, out var parsedProjectId))
            {
                var cached = await _projectsPersistence.TryGetJdArtifactAsync(parsedProjectId, bearerToken, cancellationToken);
                if (cached is not null)
                {
                    return Ok(cached);
                }
            }

            // 1. Extract text from JD file (reuse existing extraction logic)
            string rawText = _parser.ParseFile(file);

            // 2. AI Structuring for JD
            var structuredData = await _aiJdParser.ParseAsync(rawText, cancellationToken);

            if (structuredData is not null)
            {
                var formattedJson = JsonSerializer.Serialize(structuredData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                _logger.LogInformation(
                    "Parsed job description JSON output for job: {JobTitle}\n{ParsedJson}",
                    structuredData.JobTitle,
                    formattedJson);
            }
            else
            {
                _logger.LogWarning("AI JD parser returned null structured JSON output for file {FileName}.", file.FileName);
            }

            if (structuredData is not null && TryGetProjectId(projectId, out var jdProjectId))
            {
                var jdPersisted = await _projectsPersistence.UpsertJdArtifactAsync(
                    jdProjectId,
                    rawText,
                    structuredData,
                    "upload",
                    bearerToken,
                    cancellationToken);

                var wizardPersisted = await _projectsPersistence.UpsertWizardStateAsync(
                    jdProjectId,
                    "ats",
                    3,
                    new
                    {
                        jobDescriptionFileName = file.FileName,
                        hasJdArtifact = jdPersisted
                    },
                    bearerToken,
                    cancellationToken);

                Console.WriteLine($"[ParserController] JD upload persistence completed. ProjectId={jdProjectId}, JdSaved={jdPersisted}, WizardSaved={wizardPersisted}");
            }

            return Ok(structuredData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing job description file {FileName}.", file.FileName);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("job-description-text")]
    public async Task<IActionResult> ParseJobDescriptionText([FromBody] RawTextRequest request, [FromQuery] string? projectId, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RawText))
        {
            return BadRequest(new { message = "Job description text is required." });
        }

        try
        {
            var bearerToken = GetBearerToken();
            if (TryGetProjectId(projectId, out var parsedProjectId))
            {
                var cached = await _projectsPersistence.TryGetJdArtifactAsync(parsedProjectId, bearerToken, cancellationToken);
                if (cached is not null)
                {
                    return Ok(cached);
                }
            }

            var structuredData = await _aiJdParser.ParseAsync(request.RawText, cancellationToken);

            if (structuredData is not null)
            {
                var formattedJson = JsonSerializer.Serialize(structuredData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                _logger.LogInformation(
                    "Parsed job description text JSON output for job: {JobTitle}\n{ParsedJson}",
                    structuredData.JobTitle,
                    formattedJson);
            }

            if (structuredData is not null && TryGetProjectId(projectId, out var jdProjectId))
            {
                var jdPersisted = await _projectsPersistence.UpsertJdArtifactAsync(
                    jdProjectId,
                    request.RawText,
                    structuredData,
                    "paste",
                    bearerToken,
                    cancellationToken);

                var wizardPersisted = await _projectsPersistence.UpsertWizardStateAsync(
                    jdProjectId,
                    "ats",
                    3,
                    new
                    {
                        hasJdArtifact = jdPersisted,
                        sourceType = "paste"
                    },
                    bearerToken,
                    cancellationToken);

                Console.WriteLine($"[ParserController] JD text persistence completed. ProjectId={jdProjectId}, JdSaved={jdPersisted}, WizardSaved={wizardPersisted}");
            }

            return Ok(structuredData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing pasted job description text.");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("diagnostics")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult Diagnostics()
    {
        return Ok(new
        {
            status = "healthy",
            service = "ResumeAI.FileParserToJSON",
            utcNow = DateTime.UtcNow
        });
    }

    private bool TryGetProjectId(string? rawProjectId, out Guid projectId)
    {
        projectId = Guid.Empty;
        return !string.IsNullOrWhiteSpace(rawProjectId) && Guid.TryParse(rawProjectId, out projectId);
    }

    private string? GetBearerToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authHeader["Bearer ".Length..].Trim();
    }
}
