using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeAI.ATSScore.API.Data;
using ResumeAI.ATSScore.API.Persistence;
using System.Security.Claims;

namespace ResumeAI.ATSScore.API.Controllers;

[ApiController]
[Route("api/feedback")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly ProjectsDbContext _db;

    public FeedbackController(ProjectsDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitFeedback([FromBody] SubmitFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        if (IsCurrentUserAdmin())
        {
            return Forbid();
        }

        if (request.Rating < 1 || request.Rating > 10)
        {
            return BadRequest(new { message = "Rating must be between 1 and 10." });
        }

        var feedbackText = ProjectDatabaseSanitizer.SanitizeText(request.FeedbackText?.Trim());
        if (string.IsNullOrWhiteSpace(feedbackText))
        {
            return BadRequest(new { message = "Feedback text is required." });
        }

        var now = DateTime.UtcNow;
        var entity = new FeedbackEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Rating = request.Rating,
            FeedbackText = feedbackText,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        _db.Feedback.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = entity.Id,
            userId = entity.UserId,
            rating = entity.Rating,
            feedbackText = entity.FeedbackText,
            createdAt = entity.CreatedAt
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetFeedback(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var query = _db.Feedback.AsNoTracking().Where(x => !x.IsDeleted);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                id = x.Id,
                userId = x.UserId,
                rating = x.Rating,
                feedbackText = x.FeedbackText,
                createdAt = x.CreatedAt,
                updatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
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

    public record SubmitFeedbackRequest(int Rating, string FeedbackText);
}
