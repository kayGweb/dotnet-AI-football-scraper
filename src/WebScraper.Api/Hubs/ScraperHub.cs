using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WebScraper.Api.Auth;

namespace WebScraper.Api.Hubs;

/// <summary>
/// SignalR hub for real-time scrape job notifications. Clients connect with a
/// JWT (passed via the standard Authorization header or the SignalR
/// access_token query parameter for WebSocket connections), then receive
/// "ScrapeEvent" callbacks as jobs progress.
///
/// Clients should track the last-seen event Id so they can call
/// GET /api/v1/events?since= after reconnecting to replay missed events.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireViewer)]
public class ScraperHub : Hub
{
}
