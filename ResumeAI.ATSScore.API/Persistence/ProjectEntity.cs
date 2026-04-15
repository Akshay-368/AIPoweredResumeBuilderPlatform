using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class ProjectEntity
{
    [Key]
    public Guid ProjectId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "ATS";
    public string Status { get; set; } = "draft";
    public int CurrentStep { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    public ResumeArtifactEntity? ResumeArtifact { get; set; }
    public JobDescriptionArtifactEntity? JobDescriptionArtifact { get; set; }
    public WizardStateEntity? WizardState { get; set; }
    public ResumeBuilderArtifactEntity? ResumeBuilderArtifact { get; set; }
    public ICollection<AtsResultEntity> AtsResults { get; set; } = new List<AtsResultEntity>();
}
