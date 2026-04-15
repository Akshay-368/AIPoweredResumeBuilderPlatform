using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeAI.ATSScore.API.Data;

namespace ResumeAI.ATSScore.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminController : ControllerBase
{
    private readonly ProjectsDbContext _db;

    public AdminController(ProjectsDbContext db)
    {
        _db = db;
    }

    [HttpGet("overview-context")]
    public async Task<IActionResult> GetOverviewContext(CancellationToken cancellationToken)
    {
        var totalProjects = await _db.Projects.CountAsync(cancellationToken);
        var activeProjects = await _db.Projects.CountAsync(x => !x.IsDeleted, cancellationToken);
        var completedProjects = await _db.Projects.CountAsync(x => !x.IsDeleted && x.Status == "completed", cancellationToken);

        var totalNotifications = await _db.Notifications.CountAsync(x => !x.IsDeleted, cancellationToken);
        var unreadNotificationStates = await _db.NotificationUserStates.CountAsync(x => !x.IsDeletedForUser && !x.IsRead, cancellationToken);
        var totalFeedback = await _db.Feedback.CountAsync(x => !x.IsDeleted, cancellationToken);

        return Ok(new
        {
            totalProjects,
            activeProjects,
            completedProjects,
            totalNotifications,
            unreadNotificationStates,
            totalFeedback
        });
    }

    [HttpGet("users/{id:int}/activity-context")]
    public async Task<IActionResult> GetUserActivityContext([FromRoute] int id, CancellationToken cancellationToken)
    {
        var projectCount = await _db.Projects.CountAsync(x => x.UserId == id && !x.IsDeleted, cancellationToken);
        var completedProjectCount = await _db.Projects.CountAsync(x => x.UserId == id && !x.IsDeleted && x.Status == "completed", cancellationToken);
        var feedbackCount = await _db.Feedback.CountAsync(x => x.UserId == id && !x.IsDeleted, cancellationToken);

        var sentCount = await _db.Notifications.CountAsync(x => x.SenderUserId == id && !x.IsDeleted, cancellationToken);
        var receivedCount = await _db.Notifications.CountAsync(x => x.RecipientUserId == id && !x.IsDeleted, cancellationToken);

        var recentProjects = await _db.Projects
            .AsNoTracking()
            .Where(x => x.UserId == id)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(10)
            .Select(x => new
            {
                projectId = x.ProjectId,
                name = x.Name,
                type = x.Type,
                status = x.Status,
                currentStep = x.CurrentStep,
                isDeleted = x.IsDeleted,
                updatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            userId = id,
            projectCount,
            completedProjectCount,
            sentNotificationsCount = sentCount,
            receivedNotificationsCount = receivedCount,
            feedbackCount,
            recentProjects
        });
    }

    [HttpDelete("users/{id:int}/data")]
    public async Task<IActionResult> PurgeUserData([FromRoute] int id, CancellationToken cancellationToken)
    {
        var ownedProjectIds = await _db.Projects
            .Where(p => p.UserId == id)
            .Select(p => p.ProjectId)
            .ToListAsync(cancellationToken);

        if (ownedProjectIds.Count > 0)
        {
            var pdfExports = await _db.ResumePdfExports.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
            var resumeBuilderArtifacts = await _db.ResumeBuilderArtifacts.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
            var atsResults = await _db.AtsResults.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
            var wizardStates = await _db.WizardStates.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
            var jdArtifacts = await _db.JobDescriptionArtifacts.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
            var resumeArtifacts = await _db.ResumeArtifacts.Where(x => ownedProjectIds.Contains(x.ProjectId)).ToListAsync(cancellationToken);
            var projects = await _db.Projects.Where(x => x.UserId == id).ToListAsync(cancellationToken);

            if (pdfExports.Count > 0) _db.ResumePdfExports.RemoveRange(pdfExports);
            if (resumeBuilderArtifacts.Count > 0) _db.ResumeBuilderArtifacts.RemoveRange(resumeBuilderArtifacts);
            if (atsResults.Count > 0) _db.AtsResults.RemoveRange(atsResults);
            if (wizardStates.Count > 0) _db.WizardStates.RemoveRange(wizardStates);
            if (jdArtifacts.Count > 0) _db.JobDescriptionArtifacts.RemoveRange(jdArtifacts);
            if (resumeArtifacts.Count > 0) _db.ResumeArtifacts.RemoveRange(resumeArtifacts);
            if (projects.Count > 0) _db.Projects.RemoveRange(projects);
        }

        var preference = await _db.UserResumePreferences.FirstOrDefaultAsync(x => x.UserId == id, cancellationToken);
        if (preference is not null)
        {
            _db.UserResumePreferences.Remove(preference);
        }

        var feedback = await _db.Feedback.Where(x => x.UserId == id).ToListAsync(cancellationToken);
        if (feedback.Count > 0)
        {
            _db.Feedback.RemoveRange(feedback);
        }

        var notifications = await _db.Notifications
            .Where(x => x.SenderUserId == id || x.RecipientUserId == id)
            .ToListAsync(cancellationToken);

        if (notifications.Count > 0)
        {
            var notificationIds = notifications.Select(x => x.Id).ToList();
            var states = await _db.NotificationUserStates.Where(x => notificationIds.Contains(x.NotificationId)).ToListAsync(cancellationToken);
            if (states.Count > 0)
            {
                _db.NotificationUserStates.RemoveRange(states);
            }

            _db.Notifications.RemoveRange(notifications);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { userId = id, dataPurged = true });
    }

    [HttpDelete("notifications/{id:guid}")]
    public async Task<IActionResult> HardDeleteNotification([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notification is null)
        {
            return NotFound(new { message = "Notification not found." });
        }

        notification.IsDeleted = true;

        var states = await _db.NotificationUserStates.Where(x => x.NotificationId == id).ToListAsync(cancellationToken);
        foreach (var state in states)
        {
            state.IsDeletedForUser = true;
            state.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { id, deleted = true });
    }
}
