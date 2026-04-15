using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class JobDescriptionArtifactEntity
{
    [Key]
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string? RawText { get; set; }
    public string ParsedJdJson { get; set; } = "{}";
    public string SourceType { get; set; } = "upload";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    public ProjectEntity Project { get; set; } = null!;
}
