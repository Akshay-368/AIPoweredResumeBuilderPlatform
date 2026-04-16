using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
//// Also this package was added here : dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
/* 
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting; 
using System.Net;
*/

DotNetEnv.Env.Load(); // Added using the package :  dotnet add package DotNetEnv
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = Environment.GetEnvironmentVariable("Jwt__Issuer"),
            ValidAudience = Environment.GetEnvironmentVariable("Jwt__Audience"),

            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("Jwt__Key")!)
            ),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ParseResumePolicy", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", "parse:resume");
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors" , policy =>
    {
        var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN")
            ?? "http://localhost:4200";

        policy.WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();

    });
});

// 1. YARP Services added and reverse proxy config loaded from appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.UseHttpsRedirection();

// CORS Should be before auth endpoints mapping 
app.UseCors("FrontendCors");

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// 2. YARP Middleware use karo
app.MapReverseProxy();

app.Run();
