using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeAI.ATSScore.API.Data;
using ResumeAI.ATSScore.API.Persistence;
using System.Security.Claims;

namespace ResumeAI.ATSScore.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ProjectsDbContext _db;

    public NotificationsController(ProjectsDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var senderUserId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        if (request.ToUserId <= 0)
        {
            return BadRequest(new { message = "Recipient user id is required." });
        }

        if (request.ToUserId == senderUserId)
        {
            return BadRequest(new { message = "You cannot send a notification to yourself." });
        }

        var subject = ProjectDatabaseSanitizer.SanitizeText(request.Subject?.Trim());
        var body = ProjectDatabaseSanitizer.SanitizeText(request.Body?.Trim());

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            return BadRequest(new { message = "Subject and body are required." });
        }

        var now = DateTime.UtcNow;
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            SenderUserId = senderUserId,
            RecipientUserId = request.ToUserId,
            Subject = subject,
            Body = body,
            CreatedAt = now,
            IsDeleted = false
        };

        var senderState = new NotificationUserStateEntity
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            UserId = senderUserId,
            IsRead = true,
            ReadAt = now,
            IsDeletedForUser = false,
            UpdatedAt = now
        };

        var recipientState = new NotificationUserStateEntity
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            UserId = request.ToUserId,
            IsRead = false,
            ReadAt = null,
            IsDeletedForUser = false,
            UpdatedAt = now
        };

        _db.Notifications.Add(notification);
        _db.NotificationUserStates.Add(senderState);
        _db.NotificationUserStates.Add(recipientState);

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = notification.Id,
            fromUserId = notification.SenderUserId,
            toUserId = notification.RecipientUserId,
            subject = notification.Subject,
            body = notification.Body,
            createdAt = notification.CreatedAt
        });
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var isAdmin = IsCurrentUserAdmin();

        var notifications = await _db.Notifications
            .AsNoTracking()
            .Where(n => !n.IsDeleted && (isAdmin || n.RecipientUserId == currentUserId || n.SenderUserId == currentUserId))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        var states = await _db.NotificationUserStates
            .AsNoTracking()
            .Where(s => notifications.Select(n => n.Id).Contains(s.NotificationId))
            .ToListAsync(cancellationToken);

        var currentUserStateLookup = states
            .Where(s => s.UserId == currentUserId)
            .ToDictionary(s => s.NotificationId, s => s);

        var payload = notifications
            .Where(n => !currentUserStateLookup.TryGetValue(n.Id, out var state) || !state.IsDeletedForUser)
            .Select(n =>
            {
                currentUserStateLookup.TryGetValue(n.Id, out var currentState);
                var recipientState = states.FirstOrDefault(s => s.NotificationId == n.Id && s.UserId == n.RecipientUserId);

                return new
                {
                    id = n.Id,
                    fromUserId = n.SenderUserId,
                    toUserId = n.RecipientUserId,
                    subject = n.Subject,
                    body = n.Body,
                    createdAt = n.CreatedAt,
                    isRead = currentState?.IsRead ?? false,
                    readAt = currentState?.ReadAt,
                    recipientRead = recipientState?.IsRead ?? false,
                    recipientReadAt = recipientState?.ReadAt
                };
            })
            .ToList();

        return Ok(payload);
    }

    [HttpGet("sent")]
    public async Task<IActionResult> GetSent(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var isAdmin = IsCurrentUserAdmin();

        var notifications = await _db.Notifications
            .AsNoTracking()
            .Where(n => !n.IsDeleted && (isAdmin || n.SenderUserId == currentUserId || n.RecipientUserId == currentUserId))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        var states = await _db.NotificationUserStates
            .AsNoTracking()
            .Where(s => notifications.Select(n => n.Id).Contains(s.NotificationId))
            .ToListAsync(cancellationToken);

        var currentUserStateLookup = states
            .Where(s => s.UserId == currentUserId)
            .ToDictionary(s => s.NotificationId, s => s);

        var payload = notifications
            .Where(n => !currentUserStateLookup.TryGetValue(n.Id, out var state) || !state.IsDeletedForUser)
            .Select(n =>
            {
                var recipientState = states.FirstOrDefault(s => s.NotificationId == n.Id && s.UserId == n.RecipientUserId);
                return new
                {
                    id = n.Id,
                    fromUserId = n.SenderUserId,
                    toUserId = n.RecipientUserId,
                    subject = n.Subject,
                    body = n.Body,
                    createdAt = n.CreatedAt,
                    recipientRead = recipientState?.IsRead ?? false,
                    recipientReadAt = recipientState?.ReadAt
                };
            })
            .ToList();

        return Ok(payload);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);
        if (notification is null)
        {
            return NotFound(new { message = "Notification not found." });
        }

        var isAdmin = IsCurrentUserAdmin();
        if (!isAdmin && notification.RecipientUserId != currentUserId && notification.SenderUserId != currentUserId)
        {
            return Forbid();
        }

        var state = await _db.NotificationUserStates
            .FirstOrDefaultAsync(s => s.NotificationId == id && s.UserId == currentUserId, cancellationToken);

        if (state is null)
        {
            state = new NotificationUserStateEntity
            {
                Id = Guid.NewGuid(),
                NotificationId = id,
                UserId = currentUserId,
                IsRead = true,
                ReadAt = DateTime.UtcNow,
                IsDeletedForUser = false,
                UpdatedAt = DateTime.UtcNow
            };
            _db.NotificationUserStates.Add(state);
        }
        else
        {
            state.IsRead = true;
            state.ReadAt = DateTime.UtcNow;
            state.IsDeletedForUser = false;
            state.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id,
            isRead = state.IsRead,
            readAt = state.ReadAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDeleteFromCurrentUserView([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);
        if (notification is null)
        {
            return NotFound(new { message = "Notification not found." });
        }

        var isAdmin = IsCurrentUserAdmin();
        if (!isAdmin && notification.RecipientUserId != currentUserId && notification.SenderUserId != currentUserId)
        {
            return Forbid();
        }

        var state = await _db.NotificationUserStates
            .FirstOrDefaultAsync(s => s.NotificationId == id && s.UserId == currentUserId, cancellationToken);

        if (state is null)
        {
            state = new NotificationUserStateEntity
            {
                Id = Guid.NewGuid(),
                NotificationId = id,
                UserId = currentUserId,
                IsRead = false,
                ReadAt = null,
                IsDeletedForUser = true,
                UpdatedAt = DateTime.UtcNow
            };
            _db.NotificationUserStates.Add(state);
        }
        else
        {
            state.IsDeletedForUser = true;
            state.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { id, deletedForUser = true });
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid")
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        return int.TryParse(raw, out userId);
    }

    private bool IsCurrentUserAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase);
    }

    public record CreateNotificationRequest(int ToUserId, string Subject, string Body);
}
