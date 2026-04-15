using System.ComponentModel.DataAnnotations;

namespace ResumeAI.ATSScore.API.Persistence;

public class NotificationUserStateEntity
{
    [Key]
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public int UserId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
    public bool IsDeletedForUser { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public NotificationEntity Notification { get; set; } = null!;
}
