using Microsoft.EntityFrameworkCore;
using ResumeAI.Auth.API.Data;
using DotNetEnv;
using ResumeAI.Auth.API.Services;
using ResumeAI.Auth.API.Middleware;

LoadEnvironmentFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITokenService, TokenService>();

var app = builder.Build();

// Health endpoint for basic service checks.
app.MapGet("/", () => "ResumeAI Auth API is running.");

app.UseTimeLogging();

app.MapControllers();

app.Run();

static void LoadEnvironmentFile()
{
    var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

    for (var attempt = 0; attempt < 6 && currentDirectory is not null; attempt++, currentDirectory = currentDirectory.Parent)
    {
        var envPath = Path.Combine(currentDirectory.FullName, ".env");

        if (File.Exists(envPath))
        {
            Env.Load(envPath);
            return;
        }
    }
}