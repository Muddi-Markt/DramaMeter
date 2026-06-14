using Microsoft.EntityFrameworkCore;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Blazor.Services;

public interface IVoteService
{
	/// <summary>
	/// Submit a vote with the clicked level and click position.
	/// Throws <see cref="InvalidOperationException"/> if the user is still in cooldown.
	/// </summary>
	Task SubmitVoteAsync(int level, double viewBoxX, double viewBoxY, CancellationToken cancellationToken = default);

	/// <summary>
	/// Delete the current user's most recent vote.
	/// Returns false if the user has no votes.
	/// </summary>
	Task<bool> DeleteMostRecentVoteAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Delete a specific vote by ID, if it belongs to the user.
	/// Returns false if the vote was not found or does not belong to the user.
	/// </summary>
	Task<bool> DeleteVoteByIdAsync(long voteId, Guid userId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the current user's most recent vote (or null if none).
	/// </summary>
	Task<Vote?> GetUserLastVoteAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the remaining cooldown time for the current user.
	/// Returns null if the user can vote now (no cooldown).
	/// </summary>
	TimeSpan? GetCooldownRemaining(DateTimeOffset? lastVoteTime);
}

public class VoteService(
	DramaMeterDbContext dbContext,
	ISessionService sessionService) : IVoteService
{
	private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(10);

	public async Task SubmitVoteAsync(int level, double viewBoxX, double viewBoxY,
		CancellationToken cancellationToken = default)
	{
		// Validate level
		if (level is < 0 or > 3)
			throw new ArgumentOutOfRangeException(nameof(level), "Level must be between 0 and 3.");

		// Validate click angle

		// Validate viewBox coordinates
		if (viewBoxX is < 0 or > 440 || viewBoxY is < 0 or > 180)
			throw new ArgumentOutOfRangeException(nameof(viewBoxX),
				"Click position must be within the gauge SVG bounds.");

		var user = await sessionService.GetOrCreateUserAsync();

		var lastVote = await dbContext.Votes
			.Where(v => v.User.Id == user.Id)
			.OrderByDescending(v => v.CreatedAt)
			.FirstOrDefaultAsync(cancellationToken);

		if (lastVote is not null)
		{
			var elapsed = DateTime.UtcNow - lastVote.CreatedAt;
#if !DEBUG
			if (elapsed < CooldownPeriod)
				throw new InvalidOperationException(
					$"You must wait {CooldownPeriod.TotalMinutes} minutes between votes.");
#endif
		}

		var vote = new Vote
		{
			User = user,
			Level = level,
			ClickViewBoxX = viewBoxX,
			ClickViewBoxY = viewBoxY,
		};
		dbContext.Votes.Add(vote);
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task<bool> DeleteMostRecentVoteAsync(CancellationToken cancellationToken = default)
	{
		var user = await sessionService.GetOrCreateUserAsync();
		return await DeleteMostRecentVoteAsync(user.Id, cancellationToken);
	}

	private async Task<bool> DeleteMostRecentVoteAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		var vote = await dbContext.Votes
			.Where(v => v.User.Id == userId)
			.OrderByDescending(v => v.CreatedAt)
			.FirstOrDefaultAsync(cancellationToken);

		if (vote is null)
		{
			return false;
		}

		dbContext.Votes.Remove(vote);
		await dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<Vote?> GetUserLastVoteAsync(CancellationToken cancellationToken = default)
	{
		var user = await sessionService.GetOrCreateUserAsync();
		return await dbContext.Votes
			.Where(v => v.User.Id == user.Id)
			.OrderByDescending(v => v.CreatedAt)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<bool> DeleteVoteByIdAsync(long voteId, Guid userId, CancellationToken cancellationToken = default)
	{
		var vote = await dbContext.Votes
			.Where(v => v.Id == voteId && v.User.Id == userId)
			.FirstOrDefaultAsync(cancellationToken);

		if (vote is null)
		{
			return false;
		}

		dbContext.Votes.Remove(vote);
		await dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}

	public TimeSpan? GetCooldownRemaining(DateTimeOffset? lastVoteTime)
	{
		if (lastVoteTime is null)
		{
			return null;
		}

		var elapsed = DateTimeOffset.UtcNow - lastVoteTime.Value;
		if (elapsed >= CooldownPeriod)
		{
			return null;
		}

		return CooldownPeriod - elapsed;
	}
}