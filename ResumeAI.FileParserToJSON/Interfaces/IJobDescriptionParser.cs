using ResumeAI.FileParserToJson.Models;

namespace ResumeAI.FileParserToJson.Interfaces;

/// <summary>
/// Interface for parsing raw job description text into structured JSON.
/// </summary>
public interface IJobDescriptionParser
{
    /// <summary>
    /// Parse raw JD text into structured JobDescriptionData.
    /// </summary>
    /// <param name="rawText">Extracted plain text from JD file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Structured job description data</returns>
    Task<JobDescriptionData?> ParseAsync(string rawText, CancellationToken cancellationToken = default);
}
