using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class NotificationEntity
{
    [Key]
    public Guid Id { get; set; }
    public int SenderUserId { get; set; }
    public int RecipientUserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    public ICollection<NotificationUserStateEntity> UserStates { get; set; } = new List<NotificationUserStateEntity>();
}
