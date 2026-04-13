namespace ResumeAI.FileParserToJson.DTO;

/// <summary>
/// Request body for parsing pasted raw text into structured JSON.
/// </summary>
public record RawTextRequest(string RawText);