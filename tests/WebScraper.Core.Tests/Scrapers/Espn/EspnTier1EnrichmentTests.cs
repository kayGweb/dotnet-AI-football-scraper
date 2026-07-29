using System.Text.Json;
using WebScraper.Services.Scrapers.Espn;

namespace WebScraper.Tests.Scrapers.Espn;

public class EspnTier1EnrichmentTests
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
  };

  [Fact]
  public void DeserializeSummary_IncludesTier1Blocks()
  {
    const string json = """
    {
      "drives": {
        "previous": [
          {
            "id": "4015474171",
            "description": "10 plays, 75 yards, TD",
            "team": { "abbreviation": "KC" },
            "start": { "period": { "number": 1 } },
            "end": { "period": { "number": 1 } },
            "timeElapsed": { "displayValue": "5:12" },
            "yards": 75,
            "offensivePlays": 10,
            "isScore": true,
            "result": "TD",
            "displayResult": "Touchdown"
          }
        ]
      },
      "scoringPlays": [
        {
          "id": "401547417101",
          "type": { "text": "Rushing Touchdown", "abbreviation": "TD" },
          "text": "P.Mahomes 5 yd run (H.Butker kick)",
          "awayScore": 0,
          "homeScore": 7,
          "period": { "number": 1 },
          "clock": { "displayValue": "9:48" },
          "team": { "abbreviation": "KC" },
          "scoringType": { "text": "touchdown" }
        }
      ],
      "pickcenter": [
        {
          "provider": { "name": "ESPN BET" },
          "details": "KC -2.5",
          "overUnder": 47.5,
          "spread": -2.5,
          "homeTeamOdds": { "moneyLine": -140 },
          "awayTeamOdds": { "moneyLine": 120 }
        }
      ],
      "gameInfo": {
        "weather": {
          "temperature": 72,
          "highTemperature": 78,
          "displayValue": "Partly cloudy",
          "windSpeed": 8,
          "windDirection": "NW",
          "humidity": 55
        },
        "officials": [
          {
            "fullName": "Carl Cheffers",
            "position": { "displayName": "Referee" },
            "order": 1
          }
        ]
      },
      "broadcasts": [
        { "station": "CBS", "media": { "shortName": "CBS" } }
      ]
    }
    """;

    var summary = JsonSerializer.Deserialize<EspnSummaryResponse>(json, JsonOptions);

    Assert.NotNull(summary);
    Assert.NotNull(summary!.Drives?.Previous);
    Assert.Single(summary.Drives!.Previous!);
    Assert.Equal("4015474171", summary.Drives.Previous![0].Id);
    Assert.Equal(75, summary.Drives.Previous[0].Yards);

    Assert.NotNull(summary.ScoringPlays);
    Assert.Single(summary.ScoringPlays!);
    Assert.Equal(7, summary.ScoringPlays![0].HomeScore);
    Assert.Equal("P.Mahomes 5 yd run (H.Butker kick)", summary.ScoringPlays[0].Text);

    Assert.NotNull(summary.Pickcenter);
    Assert.Equal(-2.5, summary.Pickcenter![0].Spread);
    Assert.Equal(-140, summary.Pickcenter[0].HomeTeamOdds?.MoneyLine);

    Assert.Equal(72, summary.GameInfo?.Weather?.Temperature);
    Assert.Equal("Carl Cheffers", summary.GameInfo?.Officials?[0].FullName);
    Assert.Equal("CBS", summary.Broadcasts?[0].Station);
  }

  [Fact]
  public void DeserializeScoreboard_IncludesBroadcasts()
  {
    const string json = """
    {
      "events": [
        {
          "id": "401547417",
          "competitions": [
            {
              "broadcasts": [
                {
                  "market": { "type": "National" },
                  "names": ["CBS", "Paramount+"]
                }
              ],
              "competitors": []
            }
          ]
        }
      ]
    }
    """;

    var scoreboard = JsonSerializer.Deserialize<EspnScoreboardResponse>(json, JsonOptions);

    Assert.NotNull(scoreboard);
    var broadcasts = scoreboard!.Events[0].Competitions[0].Broadcasts;
    Assert.NotNull(broadcasts);
    Assert.Equal(2, broadcasts![0].Names!.Count);
  }

  [Theory]
  [InlineData(null, null)]
  [InlineData("[]", null)]
  public void FormatScoreboardBroadcasts_HandlesEmpty(string? json, string? expected)
  {
    List<EspnScoreboardBroadcast>? broadcasts = json == null
      ? null
      : JsonSerializer.Deserialize<List<EspnScoreboardBroadcast>>(json, JsonOptions);

    var result = EspnGameService.FormatScoreboardBroadcasts(broadcasts);
    Assert.Equal(expected, result);
  }

  [Fact]
  public void FormatScoreboardBroadcasts_JoinsDistinctNames()
  {
    var broadcasts = new List<EspnScoreboardBroadcast>
    {
      new() { Names = new List<string> { "CBS", "Paramount+" } },
      new() { Names = new List<string> { "CBS" } },
    };

    var result = EspnGameService.FormatScoreboardBroadcasts(broadcasts);

    Assert.Equal("CBS, Paramount+", result);
  }
}
