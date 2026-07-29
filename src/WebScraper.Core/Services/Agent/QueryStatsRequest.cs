namespace WebScraper.Services.Agent;

public class QueryStatsRequest
{
    public string Dataset { get; set; } = "player_game_stats";

    public List<QueryStatsFilter> Filters { get; set; } = new();

    public List<string> GroupBy { get; set; } = new();

    public string Measure { get; set; } = "passYards";

    public string Aggregation { get; set; } = "sum";

    public string? OrderBy { get; set; }

    public bool OrderDescending { get; set; } = true;

    public int Limit { get; set; } = 25;
}

public class QueryStatsFilter
{
    public string Field { get; set; } = string.Empty;

    public string Op { get; set; } = "=";

    public string Value { get; set; } = string.Empty;
}
