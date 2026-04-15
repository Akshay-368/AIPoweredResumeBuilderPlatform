using Microsoft.EntityFrameworkCore;
using ResumeAI.Auth.API.Data;
using DotNetEnv;
using ResumeAI.Auth.API.Services;
using ResumeAI.Auth.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

LoadEnvironmentFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Registering Swagger services for API documentation and testing.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(NormalizePostgresConnectionString(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"))));

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, MailKitEmailService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddHttpContextAccessor();

string jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? Environment.GetEnvironmentVariable("Jwt__Issuer")
    ?? throw new InvalidOperationException("Missing Jwt__Issuer / Jwt:Issuer configuration.");

string jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? Environment.GetEnvironmentVariable("Jwt__Audience")
    ?? throw new InvalidOperationException("Missing Jwt__Audience / Jwt:Audience configuration.");

string jwtKey = builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("Jwt__Key")
    ?? throw new InvalidOperationException("Missing Jwt__Key / Jwt:Key configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Too many requests, try again later." }, cancellationToken: token);
    };

    options.AddPolicy("otp-fixed-window", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("register-fixed-window", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
});

var atsProjectsApiBaseUrl = builder.Configuration["AtsProjectsApi:BaseUrl"]
    ?? Environment.GetEnvironmentVariable("AtsProjectsApi__BaseUrl")
    ?? "http://localhost:5050";

builder.Services.AddHttpClient("AtsProjectsApi", client =>
{
    client.BaseAddress = new Uri(atsProjectsApiBaseUrl);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AuthDbMigration");
        logger.LogWarning(ex, "Auth database migration skipped due to startup DB issue. API will continue running.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health endpoint for basic service checks.
app.MapGet("/", () => "ResumeAI Auth API is running.");
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.UseTimeLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string NormalizePostgresConnectionString(string? rawConnectionString)
{
    if (string.IsNullOrWhiteSpace(rawConnectionString))
        throw new InvalidOperationException("DefaultConnection is not configured.");

    if (rawConnectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
        return rawConnectionString;

    if (Uri.TryCreate(rawConnectionString, UriKind.Absolute, out var uri)
        && (uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase)))
    {
        var database = uri.AbsolutePath.Trim('/');

        var username = string.Empty;
        var password = string.Empty;

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var userParts = uri.UserInfo.Split(':', 2);
            username = Uri.UnescapeDataString(userParts[0]);
            if (userParts.Length > 1)
                password = Uri.UnescapeDataString(userParts[1]);
        }

        var port = uri.IsDefaultPort ? 5432 : uri.Port;

        return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
    }

    return rawConnectionString;
}

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
