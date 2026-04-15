using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeAI.Auth.API.Data;
using ResumeAI.Auth.API.DTOs;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace ResumeAI.Auth.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AuthDbContext _db;
    private static readonly Regex PhoneDigitsRegex = new("^\\d{10}$", RegexOptions.Compiled);

    public UsersController(AuthDbContext db)
    {
        _db = db;
    }

    [HttpGet("directory")]
    public async Task<IActionResult> GetDirectory(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var users = await _db.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.UserId)
            .Select(x => new
            {
                userId = x.UserId,
                role = x.Role
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var user = await _db.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .Select(x => new UserProfileDto(
                x.UserId,
                x.FullName,
                x.Email,
                x.PhoneNumber,
                x.Role,
                x.Provider
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(user);
    }

    [HttpPut("profile/phone-number")]
    public async Task<IActionResult> UpdatePhoneNumber([FromBody] UpdatePhoneNumberDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        if (dto is null)
        {
            return BadRequest(new { message = "Phone number is required." });
        }

        var normalizedPhoneNumber = NormalizePhoneNumber(dto.PhoneNumber);
        if (!PhoneDigitsRegex.IsMatch(normalizedPhoneNumber))
        {
            return BadRequest(new { message = "Phone number must be exactly 10 digits." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive, cancellationToken);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var isPhoneInUse = await _db.Users.AnyAsync(
            x => x.UserId != userId && x.PhoneNumber == normalizedPhoneNumber,
            cancellationToken);

        if (isPhoneInUse)
        {
            return Conflict(new { message = "An account with this phone number already exists." });
        }

        user.PhoneNumber = normalizedPhoneNumber;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Phone number updated successfully.",
            phoneNumber = user.PhoneNumber
        });
    }

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid")
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        return int.TryParse(raw, out userId);
    }

    private static string NormalizePhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, "[^0-9]", string.Empty).Trim();
    }
}
