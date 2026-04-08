using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResumeAI.FileParserToJson.Interfaces;
using System.Text.Json;

namespace ResumeAI.FileParserToJson.Controllers;

[ApiController]
[Route("api/parser")]
[Authorize(Policy = "CanParseResume")]
public class ParserController : ControllerBase
{
    private readonly IResumeParserService _parser;
    private readonly IGeminiService _gemini;
    private readonly ILogger<ParserController> _logger;

    public ParserController(
        IResumeParserService parser,
        IGeminiService gemini,
        ILogger<ParserController> logger)
    {
        _parser = parser;
        _gemini = gemini;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadResume(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded or file is empty." });
        }

        try 
        {
            // 1. Extract and Validate
            string rawText = _parser.ParseFile(file);

            // 2. AI Structuring
            var structuredData = await _gemini.GetStructuredJsonAsync(rawText);

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
                _logger.LogWarning("Gemini returned null structured JSON output for file {FileName}.", file.FileName);
            }

            return Ok(structuredData);
        }
        catch (Exception ex)
        {
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
}