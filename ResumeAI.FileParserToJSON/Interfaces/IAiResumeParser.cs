using ResumeAI.FileParserToJson.Models;

namespace ResumeAI.FileParserToJson.Interfaces;

public interface IAiResumeParser
{
    Task<ResumeData?> ParseAsync(string rawText, CancellationToken cancellationToken = default);
}