using System.ComponentModel;
using ModelContextProtocol.Server;

namespace WebScraper.Mcp.Tools;

[McpServerToolType]
public static class VenueTools
{
    [McpServerTool(Name = "nfl_list_venues"), Description(
        "List NFL stadiums/venues, optionally filtered by state or indoor/outdoor.")]
    public static Task<string> ListVenues(
        NflApiClient client,
        [Description("Filter by 2-letter US state code, e.g. \"CA\", \"TX\".")] string? state = null,
        [Description("Filter to indoor (true) or outdoor (false) venues.")] bool? isIndoor = null,
        [Description("Page number, 1-based. Defaults to 1.")] int page = 1,
        [Description("Items per page, 1–200. Defaults to 25.")] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => client.ListVenuesAsync(state, isIndoor, page, pageSize, cancellationToken);

    [McpServerTool(Name = "nfl_get_venue"), Description(
        "Get a single venue by primary key id.")]
    public static Task<string> GetVenue(
        NflApiClient client,
        [Description("Venue primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetVenueByIdAsync(id, cancellationToken);
}
