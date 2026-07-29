using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services.Agent;

public class DataCorrectionService
{
    private readonly AppDbContext _db;

    public DataCorrectionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DataCorrection> ProposeAsync(
        string entityType,
        int entityId,
        string field,
        string newValue,
        string rationale,
        string proposedBy,
        CancellationToken ct = default)
    {
        var oldValue = await ReadCurrentValueAsync(entityType, entityId, field, ct);

        var correction = new DataCorrection
        {
            EntityType = entityType,
            EntityId = entityId,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            Rationale = rationale,
            ProposedBy = proposedBy,
            Status = DataCorrectionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        _db.DataCorrections.Add(correction);
        await _db.SaveChangesAsync(ct);
        return correction;
    }

    public async Task<IReadOnlyList<DataCorrection>> ListAsync(
        DataCorrectionStatus? status = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        var query = _db.DataCorrections.AsNoTracking().AsQueryable();
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<DataCorrection?> ApproveAsync(long id, string resolvedBy, CancellationToken ct = default)
    {
        var correction = await _db.DataCorrections.FindAsync(new object[] { id }, ct);
        if (correction is null || correction.Status != DataCorrectionStatus.Pending)
            return correction;

        correction.Status = DataCorrectionStatus.Approved;
        correction.ResolvedAt = DateTime.UtcNow;
        correction.ResolvedBy = resolvedBy;
        await _db.SaveChangesAsync(ct);

        await ApplyAsync(correction, ct);
        return correction;
    }

    public async Task<DataCorrection?> RejectAsync(long id, string resolvedBy, CancellationToken ct = default)
    {
        var correction = await _db.DataCorrections.FindAsync(new object[] { id }, ct);
        if (correction is null || correction.Status != DataCorrectionStatus.Pending)
            return correction;

        correction.Status = DataCorrectionStatus.Rejected;
        correction.ResolvedAt = DateTime.UtcNow;
        correction.ResolvedBy = resolvedBy;
        await _db.SaveChangesAsync(ct);
        return correction;
    }

    public async Task ApplyAsync(DataCorrection correction, CancellationToken ct = default)
    {
        switch (correction.EntityType.ToLowerInvariant())
        {
            case "player":
                await ApplyPlayerFieldAsync(correction, ct);
                break;
            case "team":
                await ApplyTeamFieldAsync(correction, ct);
                break;
            case "game":
                await ApplyGameFieldAsync(correction, ct);
                break;
            default:
                throw new InvalidOperationException($"Entity type '{correction.EntityType}' is not supported for corrections.");
        }

        correction.Status = DataCorrectionStatus.Applied;
        correction.ResolvedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ApplyPlayerFieldAsync(DataCorrection correction, CancellationToken ct)
    {
        var player = await _db.Players.FindAsync(new object[] { correction.EntityId }, ct)
            ?? throw new InvalidOperationException($"Player {correction.EntityId} not found.");

        switch (correction.Field.ToLowerInvariant())
        {
            case "name": player.Name = correction.NewValue; break;
            case "position": player.Position = correction.NewValue; break;
            case "espnid": player.EspnId = correction.NewValue; break;
            default: throw new InvalidOperationException($"Field '{correction.Field}' not correctable on Player.");
        }
    }

    private async Task ApplyTeamFieldAsync(DataCorrection correction, CancellationToken ct)
    {
        var team = await _db.Teams.FindAsync(new object[] { correction.EntityId }, ct)
            ?? throw new InvalidOperationException($"Team {correction.EntityId} not found.");

        switch (correction.Field.ToLowerInvariant())
        {
            case "name": team.Name = correction.NewValue; break;
            case "city": team.City = correction.NewValue; break;
            case "conference": team.Conference = correction.NewValue; break;
            case "division": team.Division = correction.NewValue; break;
            default: throw new InvalidOperationException($"Field '{correction.Field}' not correctable on Team.");
        }
    }

    private async Task ApplyGameFieldAsync(DataCorrection correction, CancellationToken ct)
    {
        var game = await _db.Games.FindAsync(new object[] { correction.EntityId }, ct)
            ?? throw new InvalidOperationException($"Game {correction.EntityId} not found.");

        switch (correction.Field.ToLowerInvariant())
        {
            case "homescore" when int.TryParse(correction.NewValue, out var hs): game.HomeScore = hs; break;
            case "awayscore" when int.TryParse(correction.NewValue, out var aws): game.AwayScore = aws; break;
            case "gamestatus": game.GameStatus = correction.NewValue; break;
            default: throw new InvalidOperationException($"Field '{correction.Field}' not correctable on Game.");
        }
    }

    private async Task<string?> ReadCurrentValueAsync(
        string entityType, int entityId, string field, CancellationToken ct)
    {
        return entityType.ToLowerInvariant() switch
        {
            "player" => (await _db.Players.FindAsync(new object[] { entityId }, ct)) switch
            {
                null => null,
                var p => field.ToLowerInvariant() switch
                {
                    "name" => p.Name,
                    "position" => p.Position,
                    "espnid" => p.EspnId,
                    _ => null,
                },
            },
            "team" => (await _db.Teams.FindAsync(new object[] { entityId }, ct)) switch
            {
                null => null,
                var t => field.ToLowerInvariant() switch
                {
                    "name" => t.Name,
                    "city" => t.City,
                    _ => null,
                },
            },
            "game" => (await _db.Games.FindAsync(new object[] { entityId }, ct)) switch
            {
                null => null,
                var g => field.ToLowerInvariant() switch
                {
                    "homescore" => g.HomeScore?.ToString(),
                    "awayscore" => g.AwayScore?.ToString(),
                    "gamestatus" => g.GameStatus,
                    _ => null,
                },
            },
            _ => null,
        };
    }
}
