using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeAI.Auth.API.Data;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace ResumeAI.Auth.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
public class AdminController : ControllerBase
{
    private readonly AuthDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AuthDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<AdminController> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var totalUsers = await _db.Users.CountAsync(cancellationToken);
        var activeUsers = await _db.Users.CountAsync(x => x.IsActive, cancellationToken);
        var totalAdmins = await _db.Users.CountAsync(x => x.Role == "ADMIN", cancellationToken);

        object? atsContext = null;
        try
        {
            atsContext = await GetAtsContextAsync("/api/admin/overview-context", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch ATS overview context for admin dashboard.");
        }

        return Ok(new
        {
            totalUsers,
            activeUsers,
            totalAdmins,
            atsContext
        });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                userId = x.UserId,
                fullName = x.FullName,
                email = x.Email,
                phoneNumber = x.PhoneNumber,
                role = x.Role,
                subscriptionPlan = x.SubscriptionPlan,
                isActive = x.IsActive,
                provider = x.Provider,
                createdAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("users/{id:int}/activity")]
    public async Task<IActionResult> GetUserActivity([FromRoute] int id, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(x => x.UserId == id)
            .Select(x => new
            {
                userId = x.UserId,
                fullName = x.FullName,
                email = x.Email,
                phoneNumber = x.PhoneNumber,
                role = x.Role,
                subscriptionPlan = x.SubscriptionPlan,
                isActive = x.IsActive,
                provider = x.Provider,
                createdAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        object? atsActivity = null;
        try
        {
            atsActivity = await GetAtsContextAsync($"/api/admin/users/{id}/activity-context", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch ATS activity context for user {UserId}.", id);
        }

        return Ok(new
        {
            user,
            atsActivity
        });
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser([FromRoute] int id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        if (id == currentUserId)
        {
            return BadRequest(new { message = "Admin cannot delete their own account from this endpoint." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == id, cancellationToken);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        try
        {
            await CallAtsDeleteAsync($"/api/admin/users/{id}/data", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge ATS data for admin delete user {UserId}", id);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Failed to purge user project data. User deletion aborted." });
        }

        var challenges = await _db.UserOtpChallenges.Where(x => x.UserId == id).ToListAsync(cancellationToken);
        if (challenges.Count > 0)
        {
            _db.UserOtpChallenges.RemoveRange(challenges);
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { userId = id, deleted = true });
    }

    private async Task<object?> GetAtsContextAsync(string relativePath, CancellationToken cancellationToken)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient("AtsProjectsApi");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);
        var response = await client.GetAsync(relativePath, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ATS context call failed: {(int)response.StatusCode}");
        }

        var json = await response.Content.ReadFromJsonAsync<object>(cancellationToken: cancellationToken);
        return json;
    }

    private async Task CallAtsDeleteAsync(string relativePath, CancellationToken cancellationToken)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            throw new InvalidOperationException("Authorization header is required for ATS purge.");
        }

        var client = _httpClientFactory.CreateClient("AtsProjectsApi");
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);

        var response = await client.DeleteAsync(relativePath, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"ATS purge call failed: {(int)response.StatusCode}, body={body}");
        }
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid")
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        return int.TryParse(raw, out userId);
    }
}
