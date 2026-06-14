using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Blazor.Services;

public interface IVoteService
{
	/// <summary>
	///     Submit a vote with the clicked level and click position.
	///     Throws <see cref="InvalidOperationException" /> if the user is still in cooldown.
	/// </summary>
	Task SubmitVoteAsync(UserPoint userPoint, CancellationToken ct = default);

	/// <summary>
	///     Delete the current user's most recent vote.
	///     Returns false if the user has no votes.
	/// </summary>
	Task<bool> DeleteMostRecentVoteAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Delete a specific vote by ID, if it belongs to the user.
	///     Returns false if the vote was not found or does not belong to the user.
	/// </summary>
	Task<bool> DeleteVoteByIdAsync(long voteId, Guid userId, CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets the current user's most recent vote (or null if none).
	/// </summary>
	Task<Vote?> GetUserLastVoteAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Gets the remaining cooldown time for the current user.
	///     Returns null if the user can vote now (no cooldown).
	/// </summary>
	TimeSpan? GetCooldownRemaining(DateTimeOffset? lastVoteTime);
}

public class VoteService : IVoteService
{
	private readonly DramaMeterDbContext _dbContext;
	private readonly ISessionService _sessionService;
	private readonly DramaMeterSettings _settings;

	public VoteService(DramaMeterDbContext dbContext,
		ISessionService sessionService,
		IOptions<DramaMeterSettings> settings)
	{
		_dbContext = dbContext;
		_sessionService = sessionService;
		_settings = settings.Value;
	}

	public async Task SubmitVoteAsync(UserPoint userPoint,
		CancellationToken ct = default)
	{
		// Validate level
		if (userPoint.Level is < 0 or > 3)
			throw new ArgumentOutOfRangeException(nameof(userPoint), "Level must be between 0 and 3.");

		// Validate click angle

		// Validate viewBox coordinates
		if (userPoint.X is < 0 or > 440 || userPoint.Y is < 0 or > 180)
			throw new ArgumentOutOfRangeException(nameof(userPoint),
				"Click position must be within the gauge SVG bounds.");

		var user = await _dbContext.Users.FindAsync([userPoint.UserId], ct);
		if (user is null)
			throw new KeyNotFoundException("User not found: " + userPoint.UserId);

		var lastVote = await _dbContext.Votes
			.Where(v => v.User.Id == user.Id)
			.OrderByDescending(v => v.CreatedAt)
			.FirstOrDefaultAsync(ct);

		if (lastVote is not null)
		{
			var elapsed = DateTime.UtcNow - lastVote.CreatedAt;
			if (elapsed < _settings.CooldownPeriod)
				throw new InvalidOperationException(
					$"You must wait {_settings.CooldownPeriod.TotalMinutes} minutes between votes.");
		}

		var vote = new Vote
		{
			User = user,
			Level = userPoint.Level,
			ClickViewBoxX = userPoint.X,
			ClickViewBoxY = userPoint.Y
		};
		_dbContext.Votes.Add(vote);
		await _dbContext.SaveChangesAsync(ct);
	}

	public async Task<bool> DeleteMostRecentVoteAsync(CancellationToken cancellationToken = default)
	{
		var user = await _sessionService.GetOrCreateUserAsync();
		return await DeleteMostRecentVoteAsync(user.Id, cancellationToken);
	}

	public async Task<Vote?> GetUserLastVoteAsync(CancellationToken cancellationToken = default)
	{
		var user = await _sessionService.GetOrCreateUserAsync();
		return await _dbContext.Votes
			.Where(v => v.User.Id == user.Id)
			.OrderByDescending(v => v.CreatedAt)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<bool> DeleteVoteByIdAsync(long voteId, Guid userId, CancellationToken cancellationToken = default)
	{
		var vote = await _dbContext.Votes
			.Where(v => v.Id == voteId && v.User.Id == userId)
			.FirstOrDefaultAsync(cancellationToken);

		if (vote is null) return false;

		_dbContext.Votes.Remove(vote);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}

	public TimeSpan? GetCooldownRemaining(DateTimeOffset? lastVoteTime)
	{
		if (lastVoteTime is null)
			return null;

		var elapsed = DateTimeOffset.UtcNow - lastVoteTime.Value;
		if (elapsed >= _settings.CooldownPeriod)
			return null;

		return _settings.CooldownPeriod - elapsed;
	}

	private async Task<bool> DeleteMostRecentVoteAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		var vote = await _dbContext.Votes
			.Where(v => v.User.Id == userId)
			.OrderByDescending(v => v.CreatedAt)
			.FirstOrDefaultAsync(cancellationToken);

		if (vote is null) return false;

		_dbContext.Votes.Remove(vote);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}
}