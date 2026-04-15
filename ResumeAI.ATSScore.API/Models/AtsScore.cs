namespace ResumeAI.ATSScore.API.Models;

/// <summary>
/// Represents recommendation for resume improvement.
/// </summary>
public class RecommendationItem
{
    public string Category { get; set; } = "";
    public string Priority { get; set; } = "medium"; // high | medium | low
    public string Reason { get; set; } = "";
    public string Action { get; set; } = "";
}

/// <summary>
/// Represents an improved bullet point suggestion.
/// </summary>
public class BulletImprovement
{
    public string Original { get; set; } = "";
    public string Improved { get; set; } = "";
}

/// <summary>
/// Represents section-level scores.
/// </summary>
public class SectionScores
{
    public int Experience { get; set; }
    public int Projects { get; set; }
    public int Education { get; set; }
    public int Skills { get; set; }
    public int Formatting { get; set; }
}
