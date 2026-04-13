using ResumeAI.FileParserToJson.Models;

namespace ResumeAI.FileParserToJson.Interfaces;

public interface IGeminiService
{
    Task<ResumeData?> GetStructuredJsonAsync(string rawText);
}
