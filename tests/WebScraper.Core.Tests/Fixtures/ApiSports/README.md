# api-sports.io American Football v1 fixtures

Real responses captured from `https://v1.american-football.api-sports.io` on 2026-10-04/05
(Day 0 of `API_SPORTS_MIGRATION_PLAN.md`). Every ApiSports DTO and mapping test is written
against these files, not against memory of the docs.

| File | Call | Trimming |
|------|------|----------|
| `games-by-date.json` | `GET /games?date=2026-10-04` | Original returned 26 games (13 NFL + 13 NCAA). Kept all 13 NFL games and 3 NCAA games chosen to cover `FT`, `NS` (null scores) and `AOT` (overtime scores). `results` adjusted to 16 |
| `players-by-team.json` | `GET /players?season=2024&team=1` | Original returned 86 Raiders. Kept 14 that cover every `group` value seen, `number: 0`, `salary` variants (`"-"`, `"0-"`, `"($…)"`, `null`), `experience: null`, and a mangled name. `results` adjusted to 14 |

Still to capture (see the plan, §6 Day 0): `teams.json` (`/teams?league=1&season=2026`),
`game-player-stats.json` (`/games/statistics/players?id=21572`), `game-team-stats.json`
(`/games/statistics/teams?id=21572`), `injuries-team.json` (`/injuries?team=17`),
`games-season.json` (`/games?league=1&season=2026`, record `results` and the distinct `stage` values only).

## What the captures established

- **`/games?date=` is not league-scoped.** NCAA games (`league.id == 2`) come back in the same array.
  Every consumer must filter on `league.id == 1`. The NCAA `week` is a bare number (`"5"`) while the
  NFL `week` is `"Week 4"`; `stage` for NCAA is a division name, for the NFL `"Regular Season"`.
- **`teams.home` / `teams.away` carry `id`, `name`, `logo` only. No abbreviation.** Team identity is
  the api-sports numeric id, so `ApiSportsMappings` maps id → NFL abbreviation. 26 of 32 ids are in
  this file; the `/teams` capture fills in Falcons, Panthers, Browns, Lions, Saints and Steelers.
- `venue` has `name` and `city` only (no state), can be `null`, and names can be stale
  (`"Reliant Stadium"` for Houston). Venue is display data, never an identity key.
- A neutral-site game lists a nominal home team (Commanders "home" in London). `NeutralSite` is not
  exposed; infer it from the venue city not matching the home team if needed.
- `date.timestamp` is unix UTC and `date.timezone` is `"UTC"` by default. Use the timestamp.
- Quarter scores are `quarter_1..quarter_4`, `overtime` (null when no OT), `total`. A scheduled
  game (`NS`) has every score `null`.
- Players: `height` is a string like `5' 8"`, `weight` is `"203 lbs"`, `age` is given but no birth
  date, `experience` is `null` for rookies, `number` is `0` when unknown, `salary` is noise and is
  ignored. `group` doubles as roster status (`Offense`, `Defense`, `Special Teams`, `Practice Squad`,
  `Injured Reserve Or O` — truncated by the provider), so it feeds `Player.Status` / `IsActive`.
  Names are occasionally mangled (`"Jackson PowersJohnson"`); keep the provider string, match by id.
