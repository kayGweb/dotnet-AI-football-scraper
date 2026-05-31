using System.Diagnostics;
using System.Security.Claims;
using WebScraper.Api.Services;
using WebScraper.Models;

namespace WebScraper.Api.Middleware;

/// <summary>
/// Captures every request that reaches the API surface (after auth has run, so
/// we know which API key was used) and pushes an <see cref="ApiQueryLog"/>
/// into <see cref="IApiQueryLogQueue"/> for async persistence. Never blocks
/// the request thread on the DB.
/// </summary>
public class ApiQueryLoggingMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public ApiQueryLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IApiQueryLogQueue queue)
    {
        // Only log /api/* traffic — skip Swagger, health checks, static files, etc.
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        var stopwatch = Stopwatch.StartNew();
        var originalBody = context.Response.Body;
        long responseBytes = 0;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // HttpContext.Response.ContentLength is only populated for certain
            // content types; fall back to 0 when unavailable rather than wrapping
            // the body stream (which adds overhead to every request).
            if (context.Response.ContentLength.HasValue)
                responseBytes = context.Response.ContentLength.Value;

            var entry = new ApiQueryLog
            {
                Timestamp = DateTime.UtcNow,
                ApiKeyId = context.User.FindFirstValue("api_key_id"),
                ApiKeyName = context.User.FindFirstValue("api_key_name"),
                Method = context.Request.Method,
                Path = context.Request.Path.Value ?? string.Empty,
                QueryString = context.Request.QueryString.HasValue
                    ? context.Request.QueryString.Value
                    : null,
                StatusCode = context.Response.StatusCode,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                ResponseBytes = (int)Math.Min(responseBytes, int.MaxValue),
                UserAgent = context.Request.Headers.UserAgent.FirstOrDefault(),
                CorrelationId = correlationId,
            };

            queue.TryEnqueue(entry);
        }
    }
}
