using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class ResumeBuilderArtifactEntity
{
    [Key]
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string BuilderSnapshotJson { get; set; } = "{}";
    public string GeneratedResumeJson { get; set; } = "{}";
    public string GenerationModel { get; set; } = string.Empty;
    public string? LastChangeRequest { get; set; }
    public int RevisionCount { get; set; }
    public bool IsFinalized { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    public ProjectEntity Project { get; set; } = null!;
    public ResumeBuilderTemplateEntity Template { get; set; } = null!;
    public ICollection<ResumePdfExportEntity> PdfExports { get; set; } = new List<ResumePdfExportEntity>();
}