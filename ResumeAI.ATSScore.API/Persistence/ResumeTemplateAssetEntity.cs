using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class ResumeTemplateAssetEntity
{
    [Key]
    public Guid Id { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string AssetKey { get; set; } = string.Empty;
    public string MimeType { get; set; } = "image/svg+xml";
    public string Base64Data { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ResumeBuilderTemplateEntity? Template { get; set; }
}
