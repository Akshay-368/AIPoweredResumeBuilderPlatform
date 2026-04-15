namespace ResumeAI.FileParserToJson.Models;

/// <summary>
/// Represents structured job description data extracted from a JD file.
/// Designed for ATS compatibility and keyword extraction.
/// </summary>
public class JobDescriptionData
{
    /// <summary>
    /// Job title/position name
    /// </summary>
    public string JobTitle { get; set; } = string.Empty;

    /// <summary>
    /// Executive summary of the role
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// List of key responsibilities
    /// </summary>
    public List<string> Responsibilities { get; set; } = new();

    /// <summary>
    /// Required skills (must-have)
    /// </summary>
    public List<string> RequiredSkills { get; set; } = new();

    /// <summary>
    /// Preferred/nice-to-have skills
    /// </summary>
    public List<string> PreferredSkills { get; set; } = new();

    /// <summary>
    /// Technologies, frameworks, languages, tools
    /// </summary>
    public List<string> Technologies { get; set; } = new();

    /// <summary>
    /// Minimum years of experience required (if specified)
    /// </summary>
    public int? MinimumExperienceYears { get; set; }

    /// <summary>
    /// Deduplicated keywords for ATS scoring
    /// </summary>
    public List<string> Keywords { get; set; } = new();
}
