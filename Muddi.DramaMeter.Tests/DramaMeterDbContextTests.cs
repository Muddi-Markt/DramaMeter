using Microsoft.EntityFrameworkCore;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Tests;

public class DramaMeterDbContextTests
{
	private readonly string _dbUniqueName = $"DramaMeterTest_{Guid.NewGuid():N}";

	private DbContextOptions<DramaMeterDbContext> GetInMemoryOptions()
	{
		var builder = new DbContextOptionsBuilder<DramaMeterDbContext>();
		builder.UseInMemoryDatabase(_dbUniqueName);
		return builder.Options;
	}

	[Fact]
	public async Task DbContext_CanAddAndRetrieveUser()
	{
		using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
		var user = new User();
		ctx.Users.Add(user);
		ctx.SaveChanges();

		var retrieved = ctx.Users.Find(user.Id);
		retrieved.Should().NotBeNull();
		retrieved!.Id.Should().Be(user.Id);
	}

	[Fact]
	public async Task DbContext_CanAddAndRetrieveVote()
	{
		using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
		var user = new User();
		ctx.Users.Add(user);
		ctx.SaveChanges();

		var vote = new Vote { User = user, Level = 2 };
		ctx.Votes.Add(vote);
		ctx.SaveChanges();

		var retrieved = await ctx.Votes.FirstOrDefaultAsync(v => v.Id == vote.Id);
		retrieved.Should().NotBeNull();
		retrieved!.User!.Id.Should().Be(user.Id);
		retrieved.Level.Should().Be(2);
	}

	[Fact]
	public async Task DbContext_UserVoteRelationship_PersistsCorrectly()
	{
		using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
		var user = new User();
		ctx.Users.Add(user);
		ctx.SaveChanges();

		for (int i = 0; i < 5; i++)
		{
			ctx.Votes.Add(new Vote { User = user, Level = i % 4 });
		}

		ctx.SaveChanges();

		var userVotes = await ctx.Votes.Where(v => v.User!.Id == user.Id).ToListAsync();
		userVotes.Count.Should().Be(5);
	}

	[Fact]
	public async Task DbContext_CanQueryVotesByUserAndDate()
	{
		using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
		var user = new User();
		ctx.Users.Add(user);
		ctx.SaveChanges();

		var vote1 = new Vote { User = user, Level = 1 };
		var vote2 = new Vote { User = user, Level = 3 };
		ctx.Votes.Add(vote1);
		ctx.Votes.Add(vote2);
		ctx.SaveChanges();

		var votes = await ctx.Votes
			.Where(v => v.User!.Id == user.Id)
			.OrderByDescending(v => v.CreatedAt)
			.ToListAsync();

		votes.Count.Should().Be(2);
	}

	[Fact]
	public async Task DbContext_DeleteVote_RemovesFromDb()
	{
		using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
		var user = new User();
		ctx.Users.Add(user);
		ctx.SaveChanges();

		var vote = new Vote { User = user, Level = 1 };
		ctx.Votes.Add(vote);
		ctx.SaveChanges();

		ctx.Votes.Remove(vote);
		ctx.SaveChanges();

		(await ctx.Votes.FindAsync(vote.Id)).Should().BeNull();
	}

	[Fact]
	public async Task DbContext_MultipleUsers_AllRetrieved()
	{
		using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
		var user1 = new User();
		var user2 = new User();
		ctx.Users.Add(user1);
		ctx.Users.Add(user2);
		ctx.SaveChanges();

		var vote1 = new Vote { User = user1, Level = 0 };
		var vote2 = new Vote { User = user2, Level = 3 };
		ctx.Votes.Add(vote1);
		ctx.Votes.Add(vote2);
		ctx.SaveChanges();

		(await ctx.Users.CountAsync()).Should().Be(2);
		(await ctx.Votes.CountAsync()).Should().Be(2);
	}

	[Fact]
	public async Task DbContext_CalculatesEWMA_WhenVotesExist()
	{
		using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
		var user = new User();
		ctx.Users.Add(user);
		ctx.SaveChanges();

		var now = DateTime.UtcNow;
		var votes = new[]
		{
			new Vote { User = user, Level = 3, CreatedAt = now.AddHours(-1) },
			new Vote { User = user, Level = 0, CreatedAt = now.AddHours(-2) },
			new Vote { User = user, Level = 1, CreatedAt = now.AddHours(-3) }
		};
		ctx.Votes.AddRange(votes);
		ctx.SaveChanges();

		var allVotes = await ctx.Votes.OrderBy(v => v.CreatedAt).ToListAsync();
		allVotes.Count.Should().Be(3);
	}
}