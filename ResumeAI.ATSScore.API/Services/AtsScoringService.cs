using ResumeAI.ATSScore.API.DTO;
using ResumeAI.ATSScore.API.Models;
using ResumeAI.ATSScore.API.Interfaces;

namespace ResumeAI.ATSScore.API.Services;

/// <summary>
/// Orchestrator for ATS scoring that delegates to Gemini service.
/// </summary>
public class AtsScoringService : IAtsScoringService
{
    private readonly IGeminiAtsService _geminiService;
    private readonly ILogger<AtsScoringService> _logger;

    public AtsScoringService(
        IGeminiAtsService geminiService,
        ILogger<AtsScoringService> logger
    )
    {
        _geminiService = geminiService;
        _logger = logger;
    }

    public async Task<AtsScoreResponseDto> ScoreResumeAsync(
        ResumeData resumeData,
        string jobDescriptionText,
        string jobRole,
        string? customRole = null,
        CancellationToken cancellationToken = default
    )
    {
        if (resumeData == null)
        {
            throw new ArgumentNullException(nameof(resumeData), "Resume data cannot be null");
        }

        if (string.IsNullOrWhiteSpace(jobDescriptionText))
        {
            throw new ArgumentException("Job description cannot be empty", nameof(jobDescriptionText));
        }

        if (string.IsNullOrWhiteSpace(jobRole))
        {
            throw new ArgumentException("Job role cannot be empty", nameof(jobRole));
        }

        try
        {
            _logger.LogInformation("Starting ATS scoring for role: {JobRole}", jobRole);

            // Call Gemini ATS service
            var result = await _geminiService.GetAtsScoreAsync(
                resumeData,
                jobDescriptionText,
                jobRole,
                customRole,
                cancellationToken
            );

            if (result == null)
            {
                _logger.LogWarning("Gemini ATS service returned null result");
                throw new InvalidOperationException("ATS scoring service returned null result. Please try again.");
            }

            // Validate response
            if (result.OverallScore < 0 || result.OverallScore > 100)
            {
                _logger.LogWarning("Invalid overall score returned: {Score}", result.OverallScore);
            }

            _logger.LogInformation(
                "ATS scoring completed successfully. Overall Score: {OverallScore}, Keyword Match: {KeywordMatchScore}",
                result.OverallScore,
                result.KeywordMatchScore
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ATS scoring process");
            throw;
        }
    }
}
