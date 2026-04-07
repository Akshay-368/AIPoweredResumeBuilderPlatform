using ResumeAI.Auth.API.Models;
namespace ResumeAI.Auth.API.Services;

public interface ITokenService
{
    string CreateToken(User user);
    string GenerateRefreshToken();
}