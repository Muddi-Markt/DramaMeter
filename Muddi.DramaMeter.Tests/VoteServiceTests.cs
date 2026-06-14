using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;
using Muddi.DramaMeter.Blazor.Services;
using NSubstitute;

namespace Muddi.DramaMeter.Tests;

public class VoteServiceTests
{
	private DbContextOptions<DramaMeterDbContext> GetInMemoryOptions()
	{
		var builder = new DbContextOptionsBuilder<DramaMeterDbContext>();
		builder.UseInMemoryDatabase($"VoteTest_{Guid.NewGuid():N}");
		return builder.Options;
	}

	private IDbContextFactory<DramaMeterDbContext> CreateDbContextFactory()
	{
		var options = GetInMemoryOptions();
		return new TestDbContextFactory(options);
	}

	private IVoteService CreateService(IDbContextFactory<DramaMeterDbContext> factory, User user, ISessionService? sessionService = null,
		int cooldownMinutes = 10)
	{
		var sess = sessionService ?? Substitute.For<ISessionService>();
		sess.GetOrCreateUserAsync().Returns(user);
		var settings = Options.Create(GetSettings(cooldownMinutes));
		return new VoteService(factory, sess, settings);
	}

	private UserPoint CreateVotePoint(User user, int level, double x, double y) => new(user.Id, x, y, 1.0, level);

	private static DramaMeterSettings GetSettings(int cooldownMinutes = 10)
	{
		return new DramaMeterSettings { CooldownPeriod = TimeSpan.FromMinutes(cooldownMinutes) };
	}

	[Fact]
	public async Task SubmitVoteAsync_ValidLevel_SubmitsVote()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var db = factory.CreateDbContext();
		var user = new User();
		db.Users.Add(user);
		await db.SaveChangesAsync();

		var service = CreateService(factory, user);

		// Act
		await service.SubmitVoteAsync(CreateVotePoint(user, 2, 220, 50));

		// Assert
		var db2 = factory.CreateDbContext();
		var vote = await db2.Votes.Include(x => x.User).FirstOrDefaultAsync();
		vote.Should().NotBeNull();
		vote.User.Id.Should().Be(user.Id);
		vote.Level.Should().Be(2);
		vote.ClickViewBoxX.Should().Be(220);
		vote.ClickViewBoxY.Should().Be(50);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public async Task SubmitVoteAsync_AllLevels_SubmitsCorrectly(int level)
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var db = factory.CreateDbContext();
		var user = new User();
		db.Users.Add(user);
		await db.SaveChangesAsync();

		var service = CreateService(factory, user);

		// Act
		await service.SubmitVoteAsync(CreateVotePoint(user, level, 220, 50));

		// Assert
		var db2 = factory.CreateDbContext();
		var vote = await db2.Votes.FirstAsync();
		vote.Level.Should().Be(level);
	}

	[Fact]
	public async Task SubmitVoteAsync_InvalidLevel_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var db = factory.CreateDbContext();
		var user = new User();
		db.Users.Add(user);
		await db.SaveChangesAsync();

		var service = CreateService(factory, user);

		// Act & Assert
		await FluentActions.Awaiting(() => service.SubmitVoteAsync(CreateVotePoint(user, 4, 220, 50)))
			.Should().ThrowAsync<ArgumentOutOfRangeException>();
		await FluentActions.Awaiting(() => service.SubmitVoteAsync(CreateVotePoint(user, -1, 220, 50)))
			.Should().ThrowAsync<ArgumentOutOfRangeException>();
	}

	[Fact]
	public async Task SubmitVoteAsync_InCooldown_ThrowsException()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var db = factory.CreateDbContext();
		var user = new User();
		db.Users.Add(user);
		await db.SaveChangesAsync();

		var recentVote = new Vote { User = user, Level = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
		db.Votes.Add(recentVote);
		await db.SaveChangesAsync();

		var service = CreateService(factory, user);

		// Act & Assert
		var ex = await FluentActions.Awaiting(() => service.SubmitVoteAsync(CreateVotePoint(user, 0, 220, 50)))
			.Should().ThrowAsync<InvalidOperationException>();
		ex.Which.Message.Should().Contain("10");
	}

	[Fact]
	public async Task SubmitVoteAsync_CooldownExpired_Succeeds()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var db = factory.CreateDbContext();
		var user = new User();
		db.Users.Add(user);
		await db.SaveChangesAsync();

		var oldVote = new Vote { User = user, Level = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-15) };
		db.Votes.Add(oldVote);
		await db.SaveChangesAsync();

		var service = CreateService(factory, user);

		// Act
		await service.SubmitVoteAsync(CreateVotePoint(user, 3, 220, 50));

		// Assert
		var db2 = factory.CreateDbContext();
		var votes = await db2.Votes.ToListAsync();
		votes.Count.Should().Be(2);
	}

	[Fact]
	public async Task SubmitVoteAsync_MultipleUsers_VotesAttributedCorrectly()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var db = factory.CreateDbContext();
		var user1 = new User();
		var user2 = new User();
		db.Users.Add(user1);
		db.Users.Add(user2);
		await db.SaveChangesAsync();

		var service1 = CreateService(factory, user1);
		var service2 = CreateService(factory, user2);

		// Act
		await service1.SubmitVoteAsync(CreateVotePoint(user1, 1, 220, 50));
		await service2.SubmitVoteAsync(CreateVotePoint(user2, 3, 220, 50));

		// Assert
		var db2 = factory.CreateDbContext();
		var votes = await db2.Votes.OrderBy(v => v.Id).Include(vote => vote.User).ToListAsync();
		votes[0].User.Id.Should().Be(user1.Id);
		votes[1].User.Id.Should().Be(user2.Id);
	}

	[Fact]
	public async Task DeleteMostRecentVoteAsync_WithVotes_DeletesMostRecent()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var db = factory.CreateDbContext();
		var user = new User();
		db.Users.Add(user);
		await db.SaveChangesAsync();

		var vote1 = new Vote { User = user, Level = 0, CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
		var vote2 = new Vote { User = user, Level = 2, CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
		db.Votes.Add(vote1);
		db.Votes.Add(vote2);
		await db.SaveChangesAsync();

		var service = CreateService(factory, user);

		// Act
		var deleted = await service.DeleteMostRecentVoteAsync();

		// Assert
		deleted.Should().BeTrue();
		var db2 = factory.CreateDbContext();
		(await db2.Votes.ToListAsync()).Count.Should().Be(1);
		var remaining = await db2.Votes.FirstAsync();
		remaining.Level.Should().Be(0);
	}

	[Fact]
	public async Task DeleteMostRecentVoteAsync_NoVotes_ReturnsFalse()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var db = factory.CreateDbContext();
		var user = new User();
		db.Users.Add(user);
		await db.SaveChangesAsync();

		var service = CreateService(factory, user);

		// Act
		var deleted = await service.DeleteMostRecentVoteAsync();

		// Assert
		deleted.Should().BeFalse();
		var db2 = factory.CreateDbContext();
		(await db2.Votes.ToListAsync()).Count.Should().Be(0);
	}

	[Fact]
	public async Task DeleteVoteByIdAsync_WrongUser_ReturnsFalse()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var db = factory.CreateDbContext();
		var user1 = new User();
		var user2 = new User();
		db.Users.Add(user1);
		db.Users.Add(user2);
		await db.SaveChangesAsync();

		var vote = new Vote { User = user1, Level = 1 };
		db.Votes.Add(vote);
		await db.SaveChangesAsync();

		var service = CreateService(factory, user2);

		// Act
		var deleted = await service.DeleteVoteByIdAsync(vote.Id, user2.Id);

		// Assert
		deleted.Should().BeFalse();
		var db2 = factory.CreateDbContext();
		(await db2.Votes.ToListAsync()).Count.Should().Be(1);
	}

	[Fact]
	public async Task DeleteVoteByIdAsync_ValidDeletion_RemovesVote()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var db = factory.CreateDbContext();
		var user = new User();
		db.Users.Add(user);
		await db.SaveChangesAsync();

		var vote = new Vote { User = user, Level = 2 };
		db.Votes.Add(vote);
		await db.SaveChangesAsync();

		var service = CreateService(factory, user);

		// Act
		var deleted = await service.DeleteVoteByIdAsync(vote.Id, user.Id);

		// Assert
		deleted.Should().BeTrue();
		var db2 = factory.CreateDbContext();
		(await db2.Votes.ToListAsync()).Count.Should().Be(0);
	}

	[Fact]
	public void GetCooldownRemaining_NoLastVote_ReturnsNull()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var service = CreateService(factory, new User());

		// Act
		var result = service.GetCooldownRemaining(null);

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public void GetCooldownRemaining_CooldownExpired_ReturnsNull()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var service = CreateService(factory, new User());
		var lastVote = DateTime.UtcNow.AddMinutes(-15);

		// Act
		var result = service.GetCooldownRemaining(lastVote);

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public void GetCooldownRemaining_InCooldown_ReturnsRemainingTime()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var service = CreateService(factory, new User());
		var lastVote = DateTime.UtcNow.AddMinutes(-3);

		// Act
		var result = service.GetCooldownRemaining(lastVote);

		// Assert
		result.Should().NotBeNull();
		result.Value.TotalMinutes.Should().BeApproximately(7, 0.2);
	}

	private sealed class TestDbContextFactory(DbContextOptions<DramaMeterDbContext> options) : IDbContextFactory<DramaMeterDbContext>
	{
		public DramaMeterDbContext CreateDbContext() => new DramaMeterDbContext(options);

		public IDisposable? BeginTransaction() => throw new NotImplementedException();

		public IQueryable<TElement> CreateAsyncQueryExecutor<TElement>() => throw new NotImplementedException();

		public IAsyncEnumerator<TElement> CreateAsyncEnumerator<TElement>(IQueryable<TElement> query) => throw new NotImplementedException();

		public IQueryProvider CreateQueryProvider() => throw new NotImplementedException();
	}
}
