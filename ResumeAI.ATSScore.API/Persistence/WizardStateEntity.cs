using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class WizardStateEntity
{
    [Key]
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Module { get; set; } = "ats";
    public int CurrentStep { get; set; } = 1;
    public string StateJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    public ProjectEntity Project { get; set; } = null!;
}
