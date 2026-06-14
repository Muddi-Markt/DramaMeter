using Microsoft.EntityFrameworkCore;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;
using Muddi.DramaMeter.Blazor.Services;

namespace Muddi.DramaMeter.Tests;

public class ResultServiceTests
{
	private DbContextOptions<DramaMeterDbContext> GetInMemoryOptions()
	{
		var builder = new DbContextOptionsBuilder<DramaMeterDbContext>();
		builder.UseInMemoryDatabase($"ResultTest_{Guid.NewGuid():N}");
		return builder.Options;
	}

	private DramaMeterDbContext CreateDbContext()
	{
		return new DramaMeterDbContext(GetInMemoryOptions());
	}

	private void AddUserAndVotes(DramaMeterDbContext db, params (int Level, TimeSpan AgeSince)[] votes)
	{
		var user = new User();
		db.Users.Add(user);
		db.SaveChanges();

		foreach (var (level, ageSince) in votes)
			db.Votes.Add(new Vote
			{
				User = user,
				Level = level,
				CreatedAt = DateTime.UtcNow - ageSince
			});

		db.SaveChanges();
	}

	[Fact]
	public async Task GetDramaResultAsync_NoVotes_ReturnsZero()
	{
		// Arrange
		var db = CreateDbContext();
		var service = new ResultService(db);

		// Act
		var result = await service.GetDramaResultAsync();

		// Assert
		result.DramaLevel.Should().Be(0.0);
		result.TotalVoteCount.Should().Be(0);
	}

	[Fact]
	public async Task GetDramaResultAsync_AllNoDrama_ReturnsZero()
	{
		// Arrange
		var db = CreateDbContext();
		AddUserAndVotes(db, (0, TimeSpan.Zero), (0, TimeSpan.FromHours(1)));
		var service = new ResultService(db);

		// Act
		var result = await service.GetDramaResultAsync();

		// Assert
		result.DramaLevel.Should().Be(0.0);
	}

	[Fact]
	public async Task GetDramaResultAsync_AllExtraordinary_ReturnsThree()
	{
		// Arrange
		var db = CreateDbContext();
		AddUserAndVotes(db, (3, TimeSpan.Zero), (3, TimeSpan.FromHours(1)));
		var service = new ResultService(db);

		// Act
		var result = await service.GetDramaResultAsync();

		// Assert
		result.DramaLevel.Should().BeApproximately(3.0, 1e-6);
	}

	[Fact]
	public async Task GetDramaResultAsync_MixedLevels_ReturnsWeightedAverage()
	{
		// Arrange
		var db = CreateDbContext();
		AddUserAndVotes(db,
			(0, TimeSpan.Zero), // No Drama - fresh vote, weight ~1.0
			(3, TimeSpan.Zero), // Extraordinary - fresh vote, weight ~1.0
			(1, TimeSpan.FromHours(1)),
			(2, TimeSpan.FromHours(1))
		);
		var service = new ResultService(db);

		// Act
		var result = await service.GetDramaResultAsync();

		// Assert - fresh votes (0 and 3) should have weight ~1, so average should be ~1.5
		result.DramaLevel.Should().BeApproximately(1.5, 0.1);
		result.TotalVoteCount.Should().Be(4);
	}

	[Fact]
	public async Task GetDramaResultAsync_FreshVoteDominates_MostlyHigherValue()
	{
		// Arrange
		var db = CreateDbContext();
		AddUserAndVotes(db,
			(3, TimeSpan.Zero), // Very fresh - dominates
			(0, TimeSpan.FromDays(1)), // Older - less weight
			(0, TimeSpan.FromDays(2)), // Older - even less
			(0, TimeSpan.FromDays(3)) // Even older - very little
		);
		var service = new ResultService(db);

		// Act
		var result = await service.GetDramaResultAsync();

		// Assert - fresh vote at level 3 should pull result above 1.0
		result.DramaLevel.Should().BeGreaterThan(1.0);
	}

	[Fact]
	public async Task GetDramaResultAsync_OldVotes_ExcludedFromEWMA()
	{
		// Arrange
		var db = CreateDbContext();
		AddUserAndVotes(db,
			(3, TimeSpan.Zero), // Fresh - included
			(0, TimeSpan.FromDays(1)), // 1 day - included
			(0, TimeSpan.FromDays(4)), // 4 days - included
			(0, TimeSpan.FromDays(7.5)) // > 7 days - excluded from calculation
		);
		var service = new ResultService(db);

		// Act
		var result = await service.GetDramaResultAsync();

		// Assert - the 7.5 day vote should be excluded from EWMA, so result > 0
		result.DramaLevel.Should().BeGreaterThan(0.0);
	}

	[Fact]
	public async Task GetDramaResultAsync_ClickPositions_ReturnsUpToHundret()
	{
		// Arrange
		var db = CreateDbContext();
		var user = new User();
		db.Users.Add(user);
		db.SaveChanges();

		for (var i = 0; i < 150; i++)
			db.Votes.Add(new Vote
			{
				User = user,
				Level = i % 4,
				ClickViewBoxX = 90 + i * 20,
				ClickViewBoxY = 50 + i * 5,
				CreatedAt = DateTime.UtcNow.AddMinutes(-i * 10)
			});

		await db.SaveChangesAsync();

		var service = new ResultService(db);

		// Act
		var result = await service.GetDramaResultAsync();

		// Assert — most recent (i=0) is first
		result.ClickPositions.Should().NotBeNull();
		result.ClickPositions!.Count.Should().Be(100);
		result.ClickPositions[0].X.Should().Be(90); // i=0: 90 + 0*20
		result.ClickPositions[0].Y.Should().Be(50); // i=0: 50 + 0*5
		result.ClickPositions[9].X.Should().Be(270); // i=9: 90 + 9*20
		result.ClickPositions[9].Y.Should().Be(95); // i=9: 50 + 9*5
	}

	[Fact]
	public async Task GetDramaResultAsync_DramaDriftsToZero_WhenNoVotesForLongTime()
	{
		// Arrange
		var db = CreateDbContext();
		AddUserAndVotes(db,
			(3, TimeSpan.FromDays(8)),
			(3, TimeSpan.FromDays(9))
		);
		var service = new ResultService(db);

		// Act
		var result = await service.GetDramaResultAsync();

		// Assert - all votes > 7 days old, so excluded from EWMA, result should be 0
		result.DramaLevel.Should().Be(0.0);
	}
}