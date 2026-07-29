using WebScraper.Models;

namespace WebScraper.Api.Dtos;

public class GameDriveDto
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string EspnDriveId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string? TeamAbbreviation { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? StartPeriod { get; set; }
    public int? EndPeriod { get; set; }
    public string? TimeElapsed { get; set; }
    public int Yards { get; set; }
    public int OffensivePlays { get; set; }
    public bool IsScore { get; set; }
    public string? Result { get; set; }
    public string? DisplayResult { get; set; }
    public MetaDto Meta { get; set; } = new();
}

public class ScoringPlayDto
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string EspnPlayId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string? TeamAbbreviation { get; set; }
    public int Period { get; set; }
    public string? Clock { get; set; }
    public string PlayType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public string? ScoringType { get; set; }
    public MetaDto Meta { get; set; } = new();
}

public class GameWeatherDto
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int? TemperatureF { get; set; }
    public int? HighTemperatureF { get; set; }
    public string? Condition { get; set; }
    public int? WindSpeedMph { get; set; }
    public string? WindDirection { get; set; }
    public int? HumidityPercent { get; set; }
    public MetaDto Meta { get; set; } = new();
}

public class GameOfficialDto
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public MetaDto Meta { get; set; } = new();
}

public class GameOddsDto
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Sportsbook { get; set; } = string.Empty;
    public double? Spread { get; set; }
    public double? OverUnder { get; set; }
    public int? HomeMoneyline { get; set; }
    public int? AwayMoneyline { get; set; }
    public string SnapshotType { get; set; } = OddsSnapshotType.Current.ToString();
    public DateTime CapturedAt { get; set; }
    public string? Details { get; set; }
    public MetaDto Meta { get; set; } = new();
}
