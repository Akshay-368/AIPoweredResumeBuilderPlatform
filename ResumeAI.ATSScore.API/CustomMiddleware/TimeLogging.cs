namespace ResumeAI.ATSScore.API.Middleware;
using System.Diagnostics;


public class TimeLogging
{
    private readonly RequestDelegate _next;
    public TimeLogging ( RequestDelegate next)
    {
        _next = next;
    }
    public async Task InvokeAsync ( HttpContext context)
    {

        Stopwatch stopwatch = Stopwatch.StartNew();
        // Handing the request to the next middleware in line
        await _next (context);
        
        stopwatch.Stop(); // This is the stopwatch.Stop() method which returns void.
        var end = stopwatch.Elapsed; 
        
        Console.WriteLine($"Request{context.Request.Path} took {end.TotalMilliseconds} ms");
    }
}

public static class TimeLoggingExtensions
{
    public static IApplicationBuilder UseTimeLogging ( this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TimeLogging>();
    }
}