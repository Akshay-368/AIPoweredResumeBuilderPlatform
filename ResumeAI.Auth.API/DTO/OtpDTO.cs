namespace ResumeAI.Auth.API.DTOs;

public record ForgotPasswordRequestOtpDto(string Email);

public record VerifyOtpDto(string Email, string Otp);

public record ResetPasswordDto(string Email, string Otp, string NewPassword);

public record DeleteAccountRequestOtpDto(string Password);

public record DeleteAccountConfirmDto(string Otp);
