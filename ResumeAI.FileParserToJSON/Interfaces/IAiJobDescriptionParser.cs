using ResumeAI.FileParserToJson.Models;

namespace ResumeAI.FileParserToJson.Interfaces;

/// <summary>
/// Orchestrator interface for AI-based job description parsing.
/// Uses Gemini-based parsing.
/// </summary>
public interface IAiJobDescriptionParser
{
    /// <summary>
    /// Parse raw JD text using Gemini-based AI parsing.
    /// </summary>
    /// <param name="rawText">Extracted plain text from JD file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Structured job description data</returns>
    Task<JobDescriptionData?> ParseAsync(string rawText, CancellationToken cancellationToken = default);
}
