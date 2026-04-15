using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class FeedbackEntity
{
    [Key]
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string FeedbackText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
}
