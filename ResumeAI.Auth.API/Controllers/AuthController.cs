using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeAI.Auth.API.Data;
using ResumeAI.Auth.API.DTOs;
using ResumeAI.Auth.API.Models;
using ResumeAI.Auth.API.Services;
using System.Security.Cryptography;
using System.Text;

namespace ResumeAI.Auth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
/// <summary>
/// Handles user registration and authentication operations.
/// </summary>
public class AuthController : ControllerBase
{
    private readonly AuthDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;

    public AuthController(AuthDbContext context, ITokenService tokenService, IConfiguration configuration)
    {
        _context = context;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var normalizedRole = NormalizeRole(dto.Role);
        var normalizedPhoneNumber = NormalizePhoneNumber(dto.PhoneNumber);

        if (normalizedRole is null)
        {
            return BadRequest(new { message = "Role must be either 'user' or 'admin'." });
        }

        if (await _context.Users.AnyAsync(u => u.Email == dto.Email || u.PhoneNumber == normalizedPhoneNumber))
        {
            return BadRequest(new { message = "An account with this email or phone number already exists." });
        }

        if (normalizedRole == "ADMIN")
        {
            if (string.IsNullOrWhiteSpace(dto.AdminSecretKey))
            {
                return BadRequest(new { message = "Admin key is required for admin registration." });
            }

            if (!IsValidAdminKey(dto.AdminSecretKey))
            {
                return BadRequest(new { message = "Invalid admin key." });
            }
        }

        string salt = BCrypt.Net.BCrypt.GenerateSalt(12);
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password, salt);

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            PhoneNumber = normalizedPhoneNumber,
            PasswordHash = hashedPassword,
            Role = normalizedRole,
            SubscriptionPlan = "FREE",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Provider = "LOCAL"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Registration successful. You can now sign in." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        User? user = null;
        var normalizedPhoneNumber = NormalizePhoneNumber(dto.PhoneNumber);

        if (!string.IsNullOrWhiteSpace(normalizedPhoneNumber))
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhoneNumber);
        }
        else if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        }

        if (user == null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized("Invalid Credentials");
        }

        var token = _tokenService.CreateToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return Ok(new { Token = token, RefreshToken = refreshToken });
    }

    // --- Refresh Token Endpoint ---
[HttpPost("refresh")]
public async Task<IActionResult> Refresh([FromBody] RefreshDto? dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.RefreshToken))
    {
        return BadRequest(new { message = "Refresh token must be sent in the JSON body." });
    }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        var newJwtToken = _tokenService.CreateToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Token = newJwtToken,
            RefreshToken = newRefreshToken
        });
    }

// --- Logout Endpoint ---
[HttpPost("logout")]
public async Task<IActionResult> Logout([FromBody] LogoutDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null)
        {
            return BadRequest("User not found.");
        }

        user.RefreshToken = null;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Logged out successfully!" });
    }

    private static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return "USER";
        }

        var normalizedRole = role.Trim().ToUpperInvariant();
        return normalizedRole is "USER" or "ADMIN" ? normalizedRole : null;
    }

    private static string NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        return new string(phoneNumber.Where(char.IsDigit).ToArray());
    }

    private bool IsValidAdminKey(string providedKey)
    {
        var configuredKey = _configuration["Admin_Key"] ?? Environment.GetEnvironmentVariable("Admin_Key");

        if (string.IsNullOrWhiteSpace(providedKey) || string.IsNullOrWhiteSpace(configuredKey))
        {
            return false;
        }

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(configuredKey.Trim()));
        var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(configuredKey.Trim()));
        var providedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(providedKey.Trim()));

        return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
    }
}