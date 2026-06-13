using Microsoft.EntityFrameworkCore;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Blazor.Services;

/// <summary>
/// Represents the result of an EWMA calculation.
/// </summary>
public class DramaResult
{
    /// <summary>
    /// The EWMA drama level (0.0 to 3.0).
    /// </summary>
    public double DramaLevel { get; set; }

    /// <summary>
    /// The number of votes included in the EWMA calculation (votes within the last 7 days).
    /// </summary>
    public int TotalVoteCount { get; set; }

    /// <summary>
    /// The actual click positions (viewBox coords) of the last 10 votes, ordered by most recent first.
    /// </summary>
    public List<(double X, double Y)>? ClickPositions { get; set; }
}

public interface IResultService
{
    /// <summary>
    /// Calculates the current drama level using EWMA (Exponentially Weighted Moving Average).
    /// </summary>
    Task<DramaResult> GetDramaResultAsync(CancellationToken cancellationToken = default);
}

public class ResultService(DramaMeterDbContext dbContext) : IResultService
{
    /// <summary>
    /// Decay rate: after 3 days, weight drops to ~5%.
    /// e^(-λ × 3) = 0.05 → λ ≈ 0.998 → rounded to 1.0
    /// </summary>
    private const double Lambda = 0.998;

    /// <summary>
    /// Votes older than this have negligible weight (< 0.01%) and are excluded from calculation.
    /// </summary>
    private static readonly TimeSpan MaxRelevantAge = TimeSpan.FromDays(7);

    public async Task<DramaResult> GetDramaResultAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Load votes that are still relevant (within 7 days)
        var cutoff = now - MaxRelevantAge;
        var votes = await dbContext.Votes
            .Where(v => v.CreatedAt > cutoff)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

        var dramaResult = new DramaResult
        {
            TotalVoteCount = votes.Count
        };

        // Get the last 10 votes for display (including older ones beyond 7 days)
        var lastVotes = await dbContext.Votes
            .OrderByDescending(v => v.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);
        dramaResult.ClickPositions = lastVotes
            .Where(v => v.ClickViewBoxX.HasValue && v.ClickViewBoxY.HasValue)
            .Select(v => (v.ClickViewBoxX!.Value, v.ClickViewBoxY!.Value))
            .ToList();

        if (votes.Count == 0)
        {
            dramaResult.DramaLevel = 0.0;
            return dramaResult;
        }

        // Calculate EWMA
        double weightedSum = 0.0;
        double weightSum = 0.0;

        foreach (var vote in votes)
        {
            var ageInDays = (now - vote.CreatedAt).TotalDays;
            var weight = Math.Exp(-Lambda * ageInDays);

            weightedSum += vote.Level * weight;
            weightSum += weight;
        }

        dramaResult.DramaLevel = weightSum > 0 ? weightedSum / weightSum : 0.0;
        return dramaResult;
    }
}
