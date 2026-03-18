using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebScraper.Migrations
{
    /// <inheritdoc />
    public partial class ExpandedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── New column on Players ──
            migrationBuilder.AddColumn<string>(
                name: "EspnId",
                table: "Players",
                nullable: true);

            // ── New stat columns on PlayerGameStats ──
            migrationBuilder.AddColumn<double>(
                name: "AdjQBR",
                table: "PlayerGameStats",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DefensiveSacks",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "DefensiveTouchdowns",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExtraPointAttempts",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExtraPointsMade",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FieldGoalAttempts",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FieldGoalsMade",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Fumbles",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FumblesLost",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FumblesRecovered",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "GrossAvgPuntYards",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "InterceptionTouchdowns",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InterceptionYards",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InterceptionsCaught",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KickReturnTouchdowns",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KickReturnYards",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KickReturns",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongFieldGoal",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongKickReturn",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongPunt",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongPuntReturn",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongReception",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LongRushing",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PassesDefended",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntReturnTouchdowns",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntReturnYards",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntReturns",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntTouchbacks",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntYards",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Punts",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntsInside20",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QBHits",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "QBRating",
                table: "PlayerGameStats",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivingTargets",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SackYardsLost",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Sacks",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SoloTackles",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TacklesForLoss",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalKickingPoints",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalTackles",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "YardsPerReception",
                table: "PlayerGameStats",
                nullable: false,
                defaultValue: 0.0);

            // ── New columns on Games ──
            migrationBuilder.AddColumn<int>(
                name: "Attendance",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayOT",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayQ1",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayQ2",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayQ3",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AwayQ4",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EspnEventId",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameStatus",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeOT",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeQ1",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeQ2",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeQ3",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeQ4",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HomeWinner",
                table: "Games",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeutralSite",
                table: "Games",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VenueId",
                table: "Games",
                nullable: true);

            // ── New tables ──
            migrationBuilder.CreateTable(
                name: "ApiLinks",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Url = table.Column<string>(nullable: false),
                    EndpointType = table.Column<string>(nullable: false),
                    RelationType = table.Column<string>(nullable: false),
                    GameId = table.Column<int>(nullable: true),
                    TeamId = table.Column<int>(nullable: true),
                    Season = table.Column<int>(nullable: true),
                    Week = table.Column<int>(nullable: true),
                    EspnEventId = table.Column<string>(nullable: true),
                    DiscoveredAt = table.Column<DateTime>(nullable: false),
                    LastAccessedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiLinks_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApiLinks_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Injuries",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<int>(nullable: false),
                    PlayerId = table.Column<int>(nullable: true),
                    EspnAthleteId = table.Column<string>(nullable: false),
                    PlayerName = table.Column<string>(nullable: false),
                    Status = table.Column<string>(nullable: false),
                    InjuryType = table.Column<string>(nullable: false),
                    BodyLocation = table.Column<string>(nullable: false),
                    Side = table.Column<string>(nullable: false),
                    Detail = table.Column<string>(nullable: false),
                    ReturnDate = table.Column<DateTime>(nullable: true),
                    ReportDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Injuries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Injuries_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Injuries_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeamGameStats",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<int>(nullable: false),
                    TeamId = table.Column<int>(nullable: false),
                    FirstDowns = table.Column<int>(nullable: false),
                    FirstDownsPassing = table.Column<int>(nullable: false),
                    FirstDownsRushing = table.Column<int>(nullable: false),
                    FirstDownsPenalty = table.Column<int>(nullable: false),
                    ThirdDownMade = table.Column<int>(nullable: false),
                    ThirdDownAttempts = table.Column<int>(nullable: false),
                    FourthDownMade = table.Column<int>(nullable: false),
                    FourthDownAttempts = table.Column<int>(nullable: false),
                    TotalPlays = table.Column<int>(nullable: false),
                    TotalYards = table.Column<int>(nullable: false),
                    NetPassingYards = table.Column<int>(nullable: false),
                    PassCompletions = table.Column<int>(nullable: false),
                    PassAttempts = table.Column<int>(nullable: false),
                    YardsPerPass = table.Column<double>(nullable: false),
                    InterceptionsThrown = table.Column<int>(nullable: false),
                    SacksAgainst = table.Column<int>(nullable: false),
                    SackYardsLost = table.Column<int>(nullable: false),
                    RushingYards = table.Column<int>(nullable: false),
                    RushingAttempts = table.Column<int>(nullable: false),
                    YardsPerRush = table.Column<double>(nullable: false),
                    RedZoneMade = table.Column<int>(nullable: false),
                    RedZoneAttempts = table.Column<int>(nullable: false),
                    Turnovers = table.Column<int>(nullable: false),
                    FumblesLost = table.Column<int>(nullable: false),
                    Penalties = table.Column<int>(nullable: false),
                    PenaltyYards = table.Column<int>(nullable: false),
                    DefensiveTouchdowns = table.Column<int>(nullable: false),
                    PossessionTime = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamGameStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamGameStats_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamGameStats_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EspnId = table.Column<string>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    City = table.Column<string>(nullable: false),
                    State = table.Column<string>(nullable: false),
                    Country = table.Column<string>(nullable: false),
                    IsGrass = table.Column<bool>(nullable: false),
                    IsIndoor = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                });

            // ── Indexes ──
            migrationBuilder.CreateIndex(
                name: "IX_Games_VenueId",
                table: "Games",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLinks_GameId",
                table: "ApiLinks",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLinks_TeamId",
                table: "ApiLinks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLinks_Url",
                table: "ApiLinks",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Injuries_GameId_EspnAthleteId",
                table: "Injuries",
                columns: new[] { "GameId", "EspnAthleteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Injuries_PlayerId",
                table: "Injuries",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamGameStats_GameId_TeamId",
                table: "TeamGameStats",
                columns: new[] { "GameId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamGameStats_TeamId",
                table: "TeamGameStats",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Venues_EspnId",
                table: "Venues",
                column: "EspnId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Venues_VenueId",
                table: "Games",
                column: "VenueId",
                principalTable: "Venues",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Venues_VenueId",
                table: "Games");

            migrationBuilder.DropTable(
                name: "ApiLinks");

            migrationBuilder.DropTable(
                name: "Injuries");

            migrationBuilder.DropTable(
                name: "TeamGameStats");

            migrationBuilder.DropTable(
                name: "Venues");

            migrationBuilder.DropIndex(
                name: "IX_Games_VenueId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "EspnId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "AdjQBR",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "DefensiveSacks",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "DefensiveTouchdowns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "ExtraPointAttempts",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "ExtraPointsMade",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "FieldGoalAttempts",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "FieldGoalsMade",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Fumbles",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "FumblesLost",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "FumblesRecovered",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "GrossAvgPuntYards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "InterceptionTouchdowns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "InterceptionYards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "InterceptionsCaught",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "KickReturnTouchdowns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "KickReturnYards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "KickReturns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongFieldGoal",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongKickReturn",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongPunt",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongPuntReturn",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongReception",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "LongRushing",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PassesDefended",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntReturnTouchdowns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntReturnYards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntReturns",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntTouchbacks",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntYards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Punts",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "PuntsInside20",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "QBHits",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "QBRating",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "ReceivingTargets",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "SackYardsLost",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Sacks",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "SoloTackles",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "TacklesForLoss",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "TotalKickingPoints",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "TotalTackles",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "YardsPerReception",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Attendance",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AwayOT",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AwayQ1",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AwayQ2",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AwayQ3",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AwayQ4",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "EspnEventId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "GameStatus",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeOT",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeQ1",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeQ2",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeQ3",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeQ4",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "HomeWinner",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "NeutralSite",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "Games");
        }
    }
}
