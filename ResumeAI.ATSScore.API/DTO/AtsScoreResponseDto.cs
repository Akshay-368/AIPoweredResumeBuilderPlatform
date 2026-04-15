using ResumeAI.ATSScore.API.Models;

namespace ResumeAI.ATSScore.API.DTO;

/// <summary>
/// Response containing ATS score and recommendations.
/// </summary>
public record AtsScoreResponseDto(
    int OverallScore,
    int KeywordMatchScore,
    SectionScores SectionScores,
    List<string> MissingKeywords,
    List<string> StrongKeywords,
    List<RecommendationItem> Recommendations,
    List<BulletImprovement> ImprovedBulletSuggestions,
    string Summary
);
