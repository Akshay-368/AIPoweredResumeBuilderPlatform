using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class ResumePdfExportEntity
{
    [Key]
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ArtifactId { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string RenderOptionsJson { get; set; } = "{}";
    public byte[] PdfBytes { get; set; } = Array.Empty<byte>();
    public string? Sha256 { get; set; }
    public string FileName { get; set; } = "resume.pdf";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    public ProjectEntity Project { get; set; } = null!;
    public ResumeBuilderArtifactEntity? Artifact { get; set; }
}