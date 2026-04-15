using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ResumeAI.Auth.API.Data;
using ResumeAI.Auth.API.DTOs;
using ResumeAI.Auth.API.Models;
using ResumeAI.Auth.API.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Text;
using Google.Apis.Auth;

namespace ResumeAI.Auth.API.Controllers;

[ApiController]
[Route("api/[controller]")]
/// <summary>
/// Handles user registration and authentication operations.
/// </summary>
public class AuthController : ControllerBase
{
    private const string ForgotPasswordPurpose = "forgot_password";
    private const string DeleteAccountPurpose = "delete_account";
    private const int OtpMaxAttempts = 5;

    private readonly AuthDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly IOtpService _otpService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthDbContext context,
        ITokenService tokenService,
        IConfiguration configuration,
        IEmailService emailService,
        IOtpService otpService,
        IHttpClientFactory httpClientFactory,
        ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _configuration = configuration;
        _emailService = emailService;
        _otpService = otpService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [EnableRateLimiting("register-fixed-window")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var normalizedRole = NormalizeRole(dto.Role);
        var normalizedPhoneNumber = NormalizePhoneNumber(dto.PhoneNumber);

        if (normalizedRole is null)
        {
            return BadRequest(new { message = "Role must be either 'user' or 'admin'." });
        }

        if (!IsPasswordPolicyValid(dto.Password))
        {
            return BadRequest(new { message = "Password must have at least one uppercase letter, one lowercase letter, one number, one special character, and minimum 8 characters." });
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

    [AllowAnonymous]
    [HttpGet("google/config")]
    public IActionResult GetGoogleClientConfig()
    {
        var clientId = _configuration["GoogleAuth:ClientId"]
            ?? Environment.GetEnvironmentVariable("GoogleAuth__ClientId")
            ?? Environment.GetEnvironmentVariable("ClientID");

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Google client id is not configured." });
        }

        return Ok(new { clientId });
    }

    [AllowAnonymous]
    [EnableRateLimiting("register-fixed-window")]
    [HttpPost("google")]
    public async Task<IActionResult> AuthenticateWithGoogle([FromBody] GoogleAuthDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.IdToken))
        {
            return BadRequest(new { message = "Google ID token is required." });
        }

        var googleClientId = _configuration["GoogleAuth:ClientId"]
            ?? Environment.GetEnvironmentVariable("GoogleAuth__ClientId")
            ?? Environment.GetEnvironmentVariable("ClientID");

        if (string.IsNullOrWhiteSpace(googleClientId))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Google client id is not configured." });
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { googleClientId }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google ID token validation failed.");
            return Unauthorized(new { message = "Invalid Google token." });
        }

        if (!payload.EmailVerified)
        {
            return Unauthorized(new { message = "Google email is not verified." });
        }

        var normalizedEmail = payload.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return BadRequest(new { message = "Google account email is missing." });
        }

        var normalizedRole = NormalizeRole(dto.Role);
        if (normalizedRole is null)
        {
            return BadRequest(new { message = "Role must be either 'user' or 'admin'." });
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

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        if (user is null)
        {
            var generatedPhone = await GenerateUniqueGooglePhoneNumberAsync();
            var randomPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            user = new User
            {
                FullName = string.IsNullOrWhiteSpace(payload.Name) ? "Google User" : payload.Name.Trim(),
                Email = normalizedEmail,
                PhoneNumber = generatedPhone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(randomPassword, BCrypt.Net.BCrypt.GenerateSalt(12)),
                Role = normalizedRole,
                SubscriptionPlan = "FREE",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                Provider = "GOOGLE"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        else
        {
            if (!user.IsActive)
            {
                return Unauthorized(new { message = "Account is inactive." });
            }

            if (normalizedRole == "ADMIN" && !string.Equals(user.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                user.Role = "ADMIN";
            }

            if (string.IsNullOrWhiteSpace(user.FullName) && !string.IsNullOrWhiteSpace(payload.Name))
            {
                user.FullName = payload.Name.Trim();
            }
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

    [EnableRateLimiting("otp-fixed-window")]
    [HttpPost("forgot-password/request-otp")]
    public async Task<IActionResult> RequestForgotPasswordOtp([FromBody] ForgotPasswordRequestOtpDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.Trim(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Ok(new { message = "If an account exists, an OTP has been sent." });
        }

        var (_, otp) = await _otpService.CreateChallengeAsync(user, ForgotPasswordPurpose, TimeSpan.FromMinutes(10), cancellationToken);

        try
        {
            await _emailService.SendAsync(
                user.Email,
                "ResumeAI OTP for Password Reset",
                $"Your OTP is {otp}. It expires in 10 minutes. If you did not request this, ignore this message.",
                cancellationToken);

            return Ok(new { message = "OTP sent to your registered email." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed sending forgot-password OTP email to {Email}", user.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to send OTP email right now. Please try again." });
        }
    }

    [EnableRateLimiting("otp-fixed-window")]
    [HttpPost("forgot-password/verify-otp")]
    public async Task<IActionResult> VerifyForgotPasswordOtp([FromBody] VerifyOtpDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Otp))
        {
            return BadRequest(new { message = "Email and OTP are required." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.Trim(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return BadRequest(new { message = "Invalid OTP challenge." });
        }

        var validation = await _otpService.ValidateAsync(user, ForgotPasswordPurpose, dto.Otp.Trim(), OtpMaxAttempts, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { message = validation.Error });
        }

        return Ok(new { message = "OTP verified." });
    }

    [EnableRateLimiting("otp-fixed-window")]
    [HttpPost("forgot-password/reset")]
    public async Task<IActionResult> ResetForgotPassword([FromBody] ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Otp) || string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            return BadRequest(new { message = "Email, OTP, and new password are required." });
        }

        if (!IsPasswordPolicyValid(dto.NewPassword))
        {
            return BadRequest(new { message = "Password must have at least one uppercase letter, one lowercase letter, one number, one special character, and minimum 8 characters." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.Trim(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return BadRequest(new { message = "Invalid OTP challenge." });
        }

        if (BCrypt.Net.BCrypt.Verify(dto.NewPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "Old password and new password can't be same." });
        }

        var validation = await _otpService.ValidateAndConsumeAsync(user, ForgotPasswordPurpose, dto.Otp.Trim(), OtpMaxAttempts, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { message = validation.Error });
        }

        var salt = BCrypt.Net.BCrypt.GenerateSalt(12);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, salt);
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Password reset successful." });
    }

    [Authorize]
    [EnableRateLimiting("otp-fixed-window")]
    [HttpPost("delete-account/request-otp")]
    public async Task<IActionResult> RequestDeleteAccountOtp([FromBody] DeleteAccountRequestOtpDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "Account not found." });
        }

        if (string.IsNullOrWhiteSpace(dto.Password) || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return BadRequest(new { message = "Password verification failed." });
        }

        var (_, otp) = await _otpService.CreateChallengeAsync(user, DeleteAccountPurpose, TimeSpan.FromMinutes(10), cancellationToken);

        try
        {
            await _emailService.SendAsync(
                user.Email,
                "ResumeAI OTP for Account Deletion",
                $"Your OTP is {otp}. It expires in 10 minutes. This code is required to permanently delete your account.",
                cancellationToken);

            return Ok(new { message = "OTP sent to your registered email." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed sending delete-account OTP email to {Email}", user.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to send OTP email right now. Please try again." });
        }
    }

    [Authorize]
    [EnableRateLimiting("otp-fixed-window")]
    [HttpPost("delete-account/confirm")]
    public async Task<IActionResult> ConfirmDeleteAccount([FromBody] DeleteAccountConfirmDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Otp))
        {
            return BadRequest(new { message = "OTP is required." });
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "Authenticated user id was not found in token." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null)
        {
            return NotFound(new { message = "Account not found." });
        }

        var validation = await _otpService.ValidateAndConsumeAsync(user, DeleteAccountPurpose, dto.Otp.Trim(), OtpMaxAttempts, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new { message = validation.Error });
        }

        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            return Unauthorized(new { message = "Authorization token is required." });
        }

        try
        {
            var atsClient = _httpClientFactory.CreateClient("AtsProjectsApi");
            atsClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);
            var purgeResponse = await atsClient.DeleteAsync("/api/projects/account/purge", cancellationToken);
            if (!purgeResponse.IsSuccessStatusCode)
            {
                var body = await purgeResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed purging ATS project data for user {UserId}. Status={StatusCode}, Body={Body}", userId, purgeResponse.StatusCode, body);
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Failed to delete owned project data. Account deletion aborted." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling ATS purge endpoint for user {UserId}", userId);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Failed to delete owned project data. Account deletion aborted." });
        }

        var challenges = await _context.UserOtpChallenges.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        if (challenges.Count > 0)
        {
            _context.UserOtpChallenges.RemoveRange(challenges);
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Account deleted permanently." });
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

    private bool TryGetCurrentUserId(out int userId)
    {
        userId = 0;

        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid")
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        return int.TryParse(raw, out userId);
    }

    private static bool IsPasswordPolicyValid(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return false;
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(ch => "!@#$%^&*?".Contains(ch));

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }

    private async Task<string> GenerateUniqueGooglePhoneNumberAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = GenerateTenDigitNumber();
            var exists = await _context.Users.AnyAsync(u => u.PhoneNumber == candidate);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique phone number placeholder for Google auth user.");
    }

    private static string GenerateTenDigitNumber()
    {
        Span<char> buffer = stackalloc char[10];
        buffer[0] = (char)('6' + RandomNumberGenerator.GetInt32(0, 4));
        for (var i = 1; i < buffer.Length; i++)
        {
            buffer[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(buffer);
    }
}