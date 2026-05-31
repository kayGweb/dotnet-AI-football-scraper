using System.Collections.Concurrent;

namespace WebScraper.Api.Middleware;

/// <summary>
/// Simple sliding-window rate limiter partitioned by API key (or IP for
/// unauthenticated requests). Returns 429 + Retry-After when the limit
/// is exceeded. Default: 60 requests per minute.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, SlidingWindow> _windows = new();

    public RateLimitingMiddleware(RequestDelegate next, int maxRequests = 60, int windowSeconds = 60)
    {
        _next = next;
        _maxRequests = maxRequests;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var key = ResolveKey(context);
        var window = _windows.GetOrAdd(key, _ => new SlidingWindow(_maxRequests, _window));

        if (!window.TryAcquire())
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "10";
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc6585#section-4",
                title = "Too Many Requests",
                status = 429,
                detail = $"Rate limit of {_maxRequests} requests per {_window.TotalSeconds}s exceeded.",
            });
            return;
        }

        await _next(context);
    }

    private static string ResolveKey(HttpContext context)
    {
        var apiKeyId = context.User?.FindFirst("api_key_id")?.Value;
        if (!string.IsNullOrEmpty(apiKeyId))
            return $"apikey:{apiKeyId}";

        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            return $"user:{userId}";

        return $"ip:{context.Connection.RemoteIpAddress}";
    }

    private class SlidingWindow
    {
        private readonly int _maxRequests;
        private readonly TimeSpan _window;
        private readonly Queue<DateTime> _timestamps = new();
        private readonly object _lock = new();

        public SlidingWindow(int maxRequests, TimeSpan window)
        {
            _maxRequests = maxRequests;
            _window = window;
        }

        public bool TryAcquire()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var cutoff = now - _window;

                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                    _timestamps.Dequeue();

                if (_timestamps.Count >= _maxRequests)
                    return false;

                _timestamps.Enqueue(now);
                return true;
            }
        }
    }
}
