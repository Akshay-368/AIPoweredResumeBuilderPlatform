using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class ResumeBuilderTemplateEntity
{
    [Key]
    public string TemplateId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public string? PreviewThumbnailBase64 { get; set; }
    public string? AssetGroupKey { get; set; }
    public string RenderContractJson { get; set; } = "{}";
    public string StyleGuideJson { get; set; } = "{}";
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ResumeBuilderArtifactEntity> Artifacts { get; set; } = new List<ResumeBuilderArtifactEntity>();
    public ICollection<ResumeTemplateAssetEntity> Assets { get; set; } = new List<ResumeTemplateAssetEntity>();
}