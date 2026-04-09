using Microsoft.Extensions.Options;
using WebScraper.Models;

namespace WebScraper.Services;

public class RateLimiterService
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly int _delayMs;

    public RateLimiterService(IOptions<ScraperSettings> settings)
    {
        _delayMs = settings.Value.RequestDelayMs;
    }

    public async Task WaitAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var elapsed = DateTime.UtcNow - _lastRequestTime;
            var remaining = TimeSpan.FromMilliseconds(_delayMs) - elapsed;

            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
