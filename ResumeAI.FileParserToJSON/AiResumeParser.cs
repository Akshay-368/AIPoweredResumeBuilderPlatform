using Microsoft.Extensions.Logging;
using ResumeAI.FileParserToJson.Interfaces;
using ResumeAI.FileParserToJson.Models;

namespace ResumeAI.FileParserToJson.Services;

public class AiResumeParser : IAiResumeParser
{
    private readonly IGeminiService _geminiService;
    private readonly ILogger<AiResumeParser> _logger;

    public AiResumeParser(
        IGeminiService geminiService,
        ILogger<AiResumeParser> logger)
    {
        _geminiService = geminiService;
        _logger = logger;
    }

    public async Task<ResumeData?> ParseAsync(string rawText, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Attempting to parse with Gemini...");
        var result = await _geminiService.GetStructuredJsonAsync(rawText);
        if (result != null)
        {
            _logger.LogInformation("Gemini parsing succeeded.");
            return result;
        }

        throw new InvalidOperationException("AI parsing failed.");
    }
}