/*
This project's only job is to extract the text from a pdf file and validate it and sanitize it from any unwanted characters and then sent to gemini api to get the valid json format output. 
Other services such as ats score analyzer will rely on this service to get the valid json format output and then do the rest of the work.
The core package PdfPig was added via dotnet add package PdfPig . 
TODO : I will have to add a warning in frontend that this service can't extract text from scanned pdf files as it relies on the text layer of the pdf file and not on the image layer. So if the user uploads a scanned pdf file then the output will be empty.So i will have to warn user for not uploading images of pdf files. and reject such files and ask them to upload again
.
Now for docx files this is the text extractor package that i downloaded 
dotnet add package DocumentFormat.OpenXml
but this one also don't work on pre-2007 doc files as they are in binary format and not in xml format. 
TODO : So i will have to add a warning for that as well and ask user to upload only docx files and not doc files.

For exponential retry with Polly , i added packages 
dotnet add package Polly.Extensions.Http
dotnet add package Microsoft.Extensions.Http.Polly
and then i will have to add the retry policy in the Program.cs file while adding the http client.
*/
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using Microsoft.Extensions.Logging;
using ResumeAI.FileParserToJson.CustomMiddleware;
using ResumeAI.FileParserToJson.Interfaces;
using ResumeAI.FileParserToJson.Services;

DotNetEnv.Env.Load(); // Added using the package :  dotnet add package DotNetEnv
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

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanParseResume", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", "parse:resume");
    });

    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireRole("ADMIN");
    });
});

var geminiBaseUrl = Environment.GetEnvironmentVariable("GEMINI_BASE_URL")
    ?? "https://generativelanguage.googleapis.com/";


builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Information);
});
// Helper method for retry policy
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // 5xx, 408, etc.
        .Or<TimeoutRejectedException>()
        .WaitAndRetryAsync(
            3,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
            
        );
}

builder.Services.AddScoped<IResumeParserService, ResumeParserService>();
builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.BaseAddress = new Uri(geminiBaseUrl);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(45)));

// Register services
builder.Services.AddScoped<IAiResumeParser, AiResumeParser>();

// Job Description Parser - Gemini HttpClient with retry policy
builder.Services.AddHttpClient<GeminiJobDescriptionParser>(client =>
{
    client.BaseAddress = new Uri(geminiBaseUrl);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(45)));

// Job Description Parser - Orchestrator
builder.Services.AddScoped<IAiJobDescriptionParser, AiJobDescriptionParser>();

var projectsApiBaseUrl = Environment.GetEnvironmentVariable("PROJECTS_API_BASE_URL")
        ?? "http://localhost:5050/";

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ProjectsPersistenceClient>(client =>
{
        client.BaseAddress = new Uri(projectsApiBaseUrl);
}).AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(45)));

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
   
//}

// Added health checkpoint
app.MapGet("/" , ()=> "Resume ai file parser to json service is running." );
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.UseHttpsRedirection();
app.UseTimeLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();



app.Run();

