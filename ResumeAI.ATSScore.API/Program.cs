using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using ResumeAI.ATSScore.API.Interfaces;
using ResumeAI.ATSScore.API.Services;
using ResumeAI.ATSScore.API.Middleware;
using ResumeAI.ATSScore.API.Data;
using QuestPDF.Infrastructure;

DotNetEnv.Env.Load();

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? Environment.GetEnvironmentVariable("Jwt__Issuer")
    ?? throw new InvalidOperationException("Missing Jwt__Issuer / Jwt:Issuer configuration.");

string jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? Environment.GetEnvironmentVariable("Jwt__Audience")
    ?? throw new InvalidOperationException("Missing Jwt__Audience / Jwt:Audience configuration.");

string jwtKey = builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("Jwt__Key")
    ?? throw new InvalidOperationException("Missing Jwt__Key / Jwt:Key configuration.");

// Add services
builder.Services.AddControllers(options =>
{
    // Parsed AI resume payloads can contain nulls for optional fields.
    // Disable implicit [Required] inference for non-nullable reference types.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
builder.Services.AddOpenApi();
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Information);
});

// JWT authentication
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

// Authorization
builder.Services.AddAuthorization();

var persistenceConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

persistenceConnectionString = NormalizePostgresConnectionString(persistenceConnectionString);

if (!string.IsNullOrWhiteSpace(persistenceConnectionString))
{
    builder.Services.AddDbContext<ProjectsDbContext>(options =>
        options.UseNpgsql(persistenceConnectionString));
}

// Retry policy for Gemini HTTP calls
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .Or<TimeoutRejectedException>()
        .WaitAndRetryAsync(
            3,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
        );
}

// Gemini HTTP client
var geminiBaseUrl = Environment.GetEnvironmentVariable("GEMINI_BASE_URL")
    ?? "https://generativelanguage.googleapis.com/";

builder.Services.AddHttpClient<IGeminiAtsService, GeminiAtsService>(client =>
{
    client.BaseAddress = new Uri(geminiBaseUrl);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(60)));

// ATS scoring services
builder.Services.AddScoped<IAtsScoringService, AtsScoringService>();
builder.Services.AddHttpClient<ResumeBuilderGeminiService>(client =>
{
    client.BaseAddress = new Uri(geminiBaseUrl);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(60)));

builder.Services.AddScoped<ResumeAI.ATSScore.API.Services.TemplateRendering.IResumeTemplateRenderer, ResumeAI.ATSScore.API.Services.TemplateRendering.DeedyResumeRenderer>();
builder.Services.AddScoped<ResumeAI.ATSScore.API.Services.TemplateRendering.IResumeTemplateRenderer, ResumeAI.ATSScore.API.Services.TemplateRendering.JakesResumeRenderer>();
builder.Services.AddScoped<ResumeAI.ATSScore.API.Services.TemplateRendering.IResumeTemplateRenderer, ResumeAI.ATSScore.API.Services.TemplateRendering.SimpleHipsterResumeRenderer>();
builder.Services.AddScoped<ResumeAI.ATSScore.API.Services.TemplateRendering.ResumeTemplateRendererRegistry>();
builder.Services.AddScoped<ResumeAI.ATSScore.API.Services.TemplateRendering.ResumeTemplateAssetResolver>();
builder.Services.AddScoped<ResumeBuilderPdfService>();

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(persistenceConnectionString))
{
    using var scope = app.Services.CreateScope();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ProjectsDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ProjectsDbMigration");
        logger.LogWarning(ex, "Projects persistence migration skipped due to startup DB issue. API will continue running.");
    }
}

// Configure pipeline
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

// Health check endpoint for this service
app.MapGet("/" , () => "Resume Ai Ats score service is running. " );
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.UseHttpsRedirection();
app.UseTimeLogging(); // Custom middleware to log request processing time
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string? NormalizePostgresConnectionString(string? rawConnectionString)
{
    if (string.IsNullOrWhiteSpace(rawConnectionString))
        return null;

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
