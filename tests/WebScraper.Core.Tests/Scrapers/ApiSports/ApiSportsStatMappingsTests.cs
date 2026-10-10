using System.Text.Json;
using WebScraper.Models;
using WebScraper.Services.Scrapers.ApiSports;

namespace WebScraper.Tests.Scrapers.ApiSports;

public class ApiSportsStatMappingsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiSports", fileName);

    private static List<ApiSportsPlayerStatsTeamBlock> LoadPlayerBlocks()
    {
        var json = File.ReadAllText(FixturePath("game-player-stats.json"));
        return JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsPlayerStatsTeamBlock>>(json, JsonOptions)!.Response;
    }

    private static List<ApiSportsTeamStatsBlock> LoadTeamBlocks()
    {
        var json = File.ReadAllText(FixturePath("game-team-stats.json"));
        return JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsTeamStatsBlock>>(json, JsonOptions)!.Response;
    }

    /// <summary>Accumulates every group a player appears in, the way the stats service does.</summary>
    private static PlayerGameStats BuildLine(ApiSportsPlayerStatsTeamBlock block, int apiPlayerId)
    {
        var stats = new PlayerGameStats();
        foreach (var group in block.Groups)
        {
            foreach (var line in group.Players.Where(p => p.Player.Id == apiPlayerId))
                ApiSportsStatMappings.ApplyPlayerGroup(stats, group.Name, ApiSportsStatMappings.ToValueMap(line.Statistics));
        }
        return stats;
    }

    [Fact]
    public void Passing_MapsCompletionsAttemptsSacksAndRating()
    {
        var chiefs = LoadPlayerBlocks().Single(b => b.Team.Id == 17);
        var mahomes = BuildLine(chiefs, 1701);

        Assert.Equal(29, mahomes.PassCompletions);
        Assert.Equal(41, mahomes.PassAttempts);
        Assert.Equal(312, mahomes.PassYards);
        Assert.Equal(3, mahomes.PassTouchdowns);
        Assert.Equal(0, mahomes.Interceptions);
        Assert.Equal(2, mahomes.Sacks);
        Assert.Equal(11, mahomes.SackYardsLost);
        Assert.Equal(112.8, mahomes.QBRating);
        Assert.Null(mahomes.AdjQBR);
    }

    [Fact]
    public void PlayerInTwoGroups_AccumulatesRushingAndReceiving()
    {
        var raiders = LoadPlayerBlocks().Single(b => b.Team.Id == 1);
        var abdullah = BuildLine(raiders, 3);

        Assert.Equal(14, abdullah.RushAttempts);
        Assert.Equal(61, abdullah.RushYards);
        Assert.Equal(1, abdullah.RushTouchdowns);
        Assert.Equal(18, abdullah.LongRushing);
        Assert.Equal(4, abdullah.Receptions);
        Assert.Equal(27, abdullah.ReceivingYards);
        Assert.Equal(5, abdullah.ReceivingTargets);
        Assert.Equal(11, abdullah.LongReception);
        Assert.Equal(6.8, abdullah.YardsPerReception);
        Assert.Equal(0, abdullah.PassAttempts);
    }

    [Fact]
    public void QuarterbackInThreeGroups_KeepsPassingRushingAndFumbles()
    {
        var raiders = LoadPlayerBlocks().Single(b => b.Team.Id == 1);
        var smith = BuildLine(raiders, 1001);

        Assert.Equal(24, smith.PassCompletions);
        Assert.Equal(268, smith.PassYards);
        Assert.Equal(3, smith.Sacks);
        Assert.Equal(19, smith.SackYardsLost);
        Assert.Equal(3, smith.RushAttempts);
        Assert.Equal(12, smith.RushYards);
        Assert.Equal(1, smith.Fumbles);
        Assert.Equal(1, smith.FumblesLost);
        Assert.Equal(0, smith.FumblesRecovered);
    }

    [Fact]
    public void Defensive_MapsTacklesFractionalSacksAndQbHits()
    {
        var raiders = LoadPlayerBlocks().Single(b => b.Team.Id == 1);
        var crosby = BuildLine(raiders, 1002);

        Assert.Equal(7, crosby.TotalTackles);
        Assert.Equal(5, crosby.SoloTackles);
        Assert.Equal(1.5, crosby.DefensiveSacks);
        Assert.Equal(2, crosby.TacklesForLoss);
        Assert.Equal(1, crosby.PassesDefended);
        Assert.Equal(4, crosby.QBHits);
        Assert.Equal(0, crosby.DefensiveTouchdowns);
    }

    [Fact]
    public void Interceptions_MapsCountYardsAndTouchdowns()
    {
        var chiefs = LoadPlayerBlocks().Single(b => b.Team.Id == 17);
        var mcduffie = BuildLine(chiefs, 1706);

        Assert.Equal(1, mcduffie.InterceptionsCaught);
        Assert.Equal(23, mcduffie.InterceptionYards);
        Assert.Equal(0, mcduffie.InterceptionTouchdowns);
    }

    [Fact]
    public void Returns_MapKickAndPuntReturnsForSamePlayer()
    {
        var raiders = LoadPlayerBlocks().Single(b => b.Team.Id == 1);
        var tucker = BuildLine(raiders, 1004);

        Assert.Equal(2, tucker.KickReturns);
        Assert.Equal(51, tucker.KickReturnYards);
        Assert.Equal(29, tucker.LongKickReturn);
        Assert.Equal(3, tucker.PuntReturns);
        Assert.Equal(22, tucker.PuntReturnYards);
        Assert.Equal(12, tucker.LongPuntReturn);
        Assert.Equal(0, tucker.PuntReturnTouchdowns);
    }

    [Fact]
    public void Kicking_SplitsMadeAttemptRatios()
    {
        var raiders = LoadPlayerBlocks().Single(b => b.Team.Id == 1);
        var carlson = BuildLine(raiders, 1005);

        Assert.Equal(2, carlson.FieldGoalsMade);
        Assert.Equal(3, carlson.FieldGoalAttempts);
        Assert.Equal(48, carlson.LongFieldGoal);
        Assert.Equal(3, carlson.ExtraPointsMade);
        Assert.Equal(3, carlson.ExtraPointAttempts);
        Assert.Equal(9, carlson.TotalKickingPoints);
    }

    [Fact]
    public void Punting_MapsAverageTouchbacksAndInside20()
    {
        var raiders = LoadPlayerBlocks().Single(b => b.Team.Id == 1);
        var cole = BuildLine(raiders, 1006);

        Assert.Equal(4, cole.Punts);
        Assert.Equal(188, cole.PuntYards);
        Assert.Equal(47.0, cole.GrossAvgPuntYards);
        Assert.Equal(1, cole.PuntTouchbacks);
        Assert.Equal(2, cole.PuntsInside20);
        Assert.Equal(56, cole.LongPunt);
    }

    [Fact]
    public void UnknownGroupOrStatName_IsIgnored()
    {
        var stats = new PlayerGameStats();
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["something_new"] = "7",
            ["yards"] = "not a number"
        };

        ApiSportsStatMappings.ApplyPlayerGroup(stats, "Scrambling", values);
        ApiSportsStatMappings.ApplyPlayerGroup(stats, "Rushing", values);

        Assert.Equal(0, stats.RushYards);
        Assert.Equal(0, stats.RushAttempts);
    }

    [Theory]
    [InlineData("Kick returns", "kick returns")]
    [InlineData("KICK_RETURNS", "kick returns")]
    [InlineData("  Passing ", "passing")]
    [InlineData(null, "")]
    public void NormalizeGroupName_TrimsLowersAndReplacesUnderscores(string? input, string expected)
    {
        Assert.Equal(expected, ApiSportsStatMappings.NormalizeGroupName(input));
    }

    [Fact]
    public void TeamStatistics_MapsEverySectionIncludingMisspelledPossession()
    {
        var raiders = LoadTeamBlocks().Single(b => b.Team.Id == 1);
        var tgs = new TeamGameStats();

        ApiSportsStatMappings.MapTeamStatistics(tgs, raiders.Statistics);

        Assert.Equal(21, tgs.FirstDowns);
        Assert.Equal(13, tgs.FirstDownsPassing);
        Assert.Equal(6, tgs.FirstDownsRushing);
        Assert.Equal(2, tgs.FirstDownsPenalty);
        Assert.Equal(6, tgs.ThirdDownMade);
        Assert.Equal(13, tgs.ThirdDownAttempts);
        Assert.Equal(1, tgs.FourthDownMade);
        Assert.Equal(2, tgs.FourthDownAttempts);
        Assert.Equal(61, tgs.TotalPlays);
        Assert.Equal(322, tgs.TotalYards);
        Assert.Equal(249, tgs.NetPassingYards);
        Assert.Equal(24, tgs.PassCompletions);
        Assert.Equal(36, tgs.PassAttempts);
        Assert.Equal(6.4, tgs.YardsPerPass);
        Assert.Equal(1, tgs.InterceptionsThrown);
        Assert.Equal(3, tgs.SacksAgainst);
        Assert.Equal(19, tgs.SackYardsLost);
        Assert.Equal(73, tgs.RushingYards);
        Assert.Equal(17, tgs.RushingAttempts);
        Assert.Equal(4.3, tgs.YardsPerRush);
        Assert.Equal(3, tgs.RedZoneMade);
        Assert.Equal(4, tgs.RedZoneAttempts);
        Assert.Equal(6, tgs.Penalties);
        Assert.Equal(45, tgs.PenaltyYards);
        Assert.Equal(2, tgs.Turnovers);
        Assert.Equal(1, tgs.FumblesLost);
        Assert.Equal(0, tgs.DefensiveTouchdowns);
        Assert.Equal("29:41", tgs.PossessionTime);
    }

    [Fact]
    public void TeamStatistics_AcceptsCorrectlySpelledPossessionAndMissingSections()
    {
        var json = """
        {
          "first_downs": { "total": "9" },
          "possession": { "total": "22:05" },
          "plays": null
        }
        """;
        var statistics = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        var tgs = new TeamGameStats();

        ApiSportsStatMappings.MapTeamStatistics(tgs, statistics);

        Assert.Equal(9, tgs.FirstDowns);
        Assert.Equal("22:05", tgs.PossessionTime);
        Assert.Equal(0, tgs.TotalPlays);
        Assert.Equal(0, tgs.PassAttempts);
    }
}
