using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class UserResumePreferenceEntity
{
    [Key]
    public int UserId { get; set; }
    public string DefaultResumeRefType { get; set; } = "";
    public string DefaultResumeRefId { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
