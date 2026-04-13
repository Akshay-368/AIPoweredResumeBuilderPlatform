using ResumeAI.FileParserToJson.Interfaces;
using ResumeAI.FileParserToJson.Models;

namespace ResumeAI.FileParserToJson.Services;

/// <summary>
/// Orchestrator for AI-based job description parsing.
/// Uses Gemini for structured job description parsing.
/// </summary>
public class AiJobDescriptionParser : IAiJobDescriptionParser
{
    private readonly IJobDescriptionParser _geminiParser;
    private readonly ILogger<AiJobDescriptionParser> _logger;

    public AiJobDescriptionParser(
        GeminiJobDescriptionParser geminiParser,
        ILogger<AiJobDescriptionParser> logger)
    {
        _geminiParser = geminiParser;
        _logger = logger;
    }

    public async Task<JobDescriptionData?> ParseAsync(string rawText, CancellationToken cancellationToken = default)
    {
        // Try Gemini first
        try
        {
            _logger.LogInformation("Attempting to parse JD with Gemini...");
            var result = await _geminiParser.ParseAsync(rawText, cancellationToken);
            if (result != null)
            {
                _logger.LogInformation("Gemini JD parsing succeeded. Found job title: {JobTitle}", result.JobTitle);
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini JD parsing failed.");
        }

        throw new InvalidOperationException("Job description parsing failed. Please try uploading again or paste the JD text instead.");
    }
}
