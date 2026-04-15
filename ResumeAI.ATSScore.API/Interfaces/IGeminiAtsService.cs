using ResumeAI.ATSScore.API.DTO;
using ResumeAI.ATSScore.API.Models;

namespace ResumeAI.ATSScore.API.Interfaces;

public interface IGeminiAtsService
{
    Task<AtsScoreResponseDto?> GetAtsScoreAsync(
        ResumeData resumeData,
        string jobDescriptionText,
        string jobRole,
        string? customRole = null,
        CancellationToken cancellationToken = default
    );
}
