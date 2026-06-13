using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;
using Muddi.DramaMeter.Blazor.Services;

namespace Muddi.DramaMeter.Tests;

public class VoteServiceTests
{
    private DbContextOptions<DramaMeterDbContext> GetInMemoryOptions()
    {
        var builder = new DbContextOptionsBuilder<DramaMeterDbContext>();
        builder.UseInMemoryDatabase($"VoteTest_{Guid.NewGuid():N}");
        return builder.Options;
    }

    private DramaMeterDbContext CreateDbContext()
    {
        return new DramaMeterDbContext(GetInMemoryOptions());
    }

    private User CreateAndSaveUser(DramaMeterDbContext db)
    {
        var user = new User();
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private IVoteService CreateService(DramaMeterDbContext db, User user)
    {
        var sessionService = Substitute.For<ISessionService>();
        sessionService.GetOrCreateUserAsync().Returns(user);
        return new VoteService(db, sessionService);
    }

    [Fact]
    public async Task SubmitVoteAsync_ValidLevel_SubmitsVote()
    {
        // Arrange
        var db = CreateDbContext();
        var user = CreateAndSaveUser(db);
        var service = CreateService(db, user);

        // Act
        await service.SubmitVoteAsync(2, 220, 50);

        // Assert
        var vote = await db.Votes.FirstOrDefaultAsync();
        vote.Should().NotBeNull();
        vote!.User!.Id.Should().Be(user.Id);
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
        var db = CreateDbContext();
        var user = CreateAndSaveUser(db);
        var service = CreateService(db, user);

        // Act
        await service.SubmitVoteAsync(level, 220, 50);

        // Assert
        var vote = await db.Votes.FirstAsync();
        vote.Level.Should().Be(level);
    }

    [Fact]
    public async Task SubmitVoteAsync_InvalidLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var db = CreateDbContext();
        var user = CreateAndSaveUser(db);
        var service = CreateService(db, user);

        // Act & Assert
        await FluentActions.Awaiting(() => service.SubmitVoteAsync(4, 220, 50))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => service.SubmitVoteAsync(-1, 220, 50))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SubmitVoteAsync_InCooldown_ThrowsException()
    {
        // Arrange
        var db = CreateDbContext();
        var user = CreateAndSaveUser(db);

        // Create a recent vote (within cooldown period)
        var recentVote = new Vote { User = user, Level = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
        db.Votes.Add(recentVote);
        await db.SaveChangesAsync();

        var service = CreateService(db, user);

        // Act & Assert
        var ex = await FluentActions.Awaiting(() => service.SubmitVoteAsync(0, 220, 50))
            .Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("10");
    }

    [Fact]
    public async Task SubmitVoteAsync_CooldownExpired_Succeeds()
    {
        // Arrange
        var db = CreateDbContext();
        var user = CreateAndSaveUser(db);

        // Create an old vote (outside cooldown period)
        var oldVote = new Vote { User = user, Level = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-15) };
        db.Votes.Add(oldVote);
        await db.SaveChangesAsync();

        var service = CreateService(db, user);

        // Act
        await service.SubmitVoteAsync(3, 220, 50);

        // Assert
        var votes = await db.Votes.ToListAsync();
        votes.Count.Should().Be(2);
    }

    [Fact]
    public async Task SubmitVoteAsync_UsesSessionService_Identity()
    {
        // Arrange
        var db = CreateDbContext();
        var user = CreateAndSaveUser(db);
        var sessionService = Substitute.For<ISessionService>();
        sessionService.GetOrCreateUserAsync().Returns(user);
        var service = new VoteService(db, sessionService);

        // Act
        await service.SubmitVoteAsync(1, 220, 50);

        // Assert — vote must be attributed to the session user, not the first user in DB
        var vote = await db.Votes.FirstAsync();
        vote!.User!.Id.Should().Be(user.Id);
        await sessionService.Received(1).GetOrCreateUserAsync();
    }

    [Fact]
    public async Task SubmitVoteAsync_MultipleUsers_VotesAttributedCorrectly()
    {
        // Arrange
        var db = CreateDbContext();
        var user1 = CreateAndSaveUser(db);
        var user2 = CreateAndSaveUser(db);

        var service1 = CreateService(db, user1);
        var service2 = CreateService(db, user2);

        // Act
        await service1.SubmitVoteAsync(1, 220, 50);
        await service2.SubmitVoteAsync(3, 220, 50);

        // Assert
        var votes = await db.Votes.OrderBy(v => v.Id).ToListAsync();
        votes[0].User!.Id.Should().Be(user1.Id);
        votes[1].User!.Id.Should().Be(user2.Id);
    }

    [Fact]
    public async Task DeleteMostRecentVoteAsync_WithVotes_DeletesMostRecent()
    {
        // Arrange
        var db = CreateDbContext();
        var user = CreateAndSaveUser(db);

        var vote1 = new Vote { User = user, Level = 0, CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
        var vote2 = new Vote { User = user, Level = 2, CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
        db.Votes.Add(vote1);
        db.Votes.Add(vote2);
        await db.SaveChangesAsync();

        var service = CreateService(db, user);

        // Act
        var deleted = await service.DeleteMostRecentVoteAsync();

        // Assert
        deleted.Should().BeTrue();
        (await db.Votes.ToListAsync()).Count.Should().Be(1);
        var remaining = await db.Votes.FirstAsync();
        remaining.Level.Should().Be(0);
    }

    [Fact]
    public async Task DeleteMostRecentVoteAsync_NoVotes_ReturnsFalse()
    {
        // Arrange
        var db = CreateDbContext();
        var user = CreateAndSaveUser(db);
        var service = CreateService(db, user);

        // Act
        var deleted = await service.DeleteMostRecentVoteAsync();

        // Assert
        deleted.Should().BeFalse();
        (await db.Votes.ToListAsync()).Count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteVoteByIdAsync_WrongUser_ReturnsFalse()
    {
        // Arrange
        var db = CreateDbContext();
        var user1 = CreateAndSaveUser(db);
        var user2 = CreateAndSaveUser(db);

        var vote = new Vote { User = user1, Level = 1 };
        db.Votes.Add(vote);
        await db.SaveChangesAsync();

        var service = CreateService(db, user2);

        // Act
        var deleted = await service.DeleteVoteByIdAsync(vote.Id, user2.Id);

        // Assert
        deleted.Should().BeFalse();
        (await db.Votes.ToListAsync()).Count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteVoteByIdAsync_ValidDeletion_RemovesVote()
    {
        // Arrange
        var db = CreateDbContext();
        var user = CreateAndSaveUser(db);

        var vote = new Vote { User = user, Level = 2 };
        db.Votes.Add(vote);
        await db.SaveChangesAsync();

        var service = CreateService(db, user);

        // Act
        var deleted = await service.DeleteVoteByIdAsync(vote.Id, user.Id);

        // Assert
        deleted.Should().BeTrue();
        (await db.Votes.ToListAsync()).Count.Should().Be(0);
    }

    [Fact]
    public void GetCooldownRemaining_NoLastVote_ReturnsNull()
    {
        // Arrange
        var db = CreateDbContext();
        var user = new User();
        var service = new VoteService(db, Substitute.For<ISessionService>());

        // Act
        var result = service.GetCooldownRemaining(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCooldownRemaining_CooldownExpired_ReturnsNull()
    {
        // Arrange
        var db = CreateDbContext();
        var service = new VoteService(db, Substitute.For<ISessionService>());
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
        var db = CreateDbContext();
        var service = new VoteService(db, Substitute.For<ISessionService>());
        var lastVote = DateTime.UtcNow.AddMinutes(-3);

        // Act
        var result = service.GetCooldownRemaining(lastVote);

        // Assert
        result.Should().NotBeNull();
        result!.Value.TotalMinutes.Should().BeApproximately(7, 0.2);
    }

}
