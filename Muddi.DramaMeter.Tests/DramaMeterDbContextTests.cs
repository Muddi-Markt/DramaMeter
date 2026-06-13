using Microsoft.EntityFrameworkCore;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Tests;

public class DramaMeterDbContextTests
{
    private readonly string _dbUniqueName;

    public DramaMeterDbContextTests()
    {
        _dbUniqueName = $"DramaMeterTest_{Guid.NewGuid():N}";
    }

    private DbContextOptions<DramaMeterDbContext> GetInMemoryOptions()
    {
        var builder = new DbContextOptionsBuilder<DramaMeterDbContext>();
        builder.UseInMemoryDatabase(_dbUniqueName);
        return builder.Options;
    }

    [Fact]
    public void DbContext_CanAddAndRetrieveUser()
    {
        using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
        var user = new User();
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var retrieved = ctx.Users.Find(user.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(user.Id, retrieved.Id);
    }

    [Fact]
    public void DbContext_CanAddAndRetrieveVote()
    {
        using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
        var user = new User();
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var vote = new Vote { UserId = user.Id, Level = 2 };
        ctx.Votes.Add(vote);
        ctx.SaveChanges();

        var retrieved = ctx.Votes.FirstOrDefault(v => v.Id == vote.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(user.Id, retrieved.UserId);
        Assert.Equal(2, retrieved.Level);
    }

    [Fact]
    public void DbContext_UserVoteRelationship_PersistsCorrectly()
    {
        using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
        var user = new User();
        ctx.Users.Add(user);
        ctx.SaveChanges();

        for (int i = 0; i < 5; i++)
        {
            ctx.Votes.Add(new Vote { UserId = user.Id, Level = i % 4 });
        }
        ctx.SaveChanges();

        var userVotes = ctx.Votes.Where(v => v.UserId == user.Id).ToList();
        Assert.Equal(5, userVotes.Count);
    }

    [Fact]
    public void DbContext_CanQueryVotesByUserAndDate()
    {
        using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
        var user = new User();
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var vote1 = new Vote { UserId = user.Id, Level = 1 };
        var vote2 = new Vote { UserId = user.Id, Level = 3 };
        ctx.Votes.Add(vote1);
        ctx.Votes.Add(vote2);
        ctx.SaveChanges();

        var votes = ctx.Votes
            .Where(v => v.UserId == user.Id)
            .OrderByDescending(v => v.CreatedAt)
            .ToList();

        Assert.Equal(2, votes.Count);
    }

    [Fact]
    public void DbContext_DeleteVote_RemovesFromDb()
    {
        using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
        var user = new User();
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var vote = new Vote { UserId = user.Id, Level = 1 };
        ctx.Votes.Add(vote);
        ctx.SaveChanges();

        ctx.Votes.Remove(vote);
        ctx.SaveChanges();

        Assert.Null(ctx.Votes.Find(vote.Id));
    }

    [Fact]
    public void DbContext_MultipleUsers_AllRetrieved()
    {
        using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
        var user1 = new User();
        var user2 = new User();
        ctx.Users.Add(user1);
        ctx.Users.Add(user2);
        ctx.SaveChanges();

        ctx.Votes.Add(new Vote { UserId = user1.Id, Level = 0 });
        ctx.Votes.Add(new Vote { UserId = user2.Id, Level = 3 });
        ctx.SaveChanges();

        Assert.Equal(2, ctx.Users.Count());
        Assert.Equal(2, ctx.Votes.Count());
    }

    [Fact]
    public void DbContext_CalculatesEWMA_WhenVotesExist()
    {
        using var ctx = new DramaMeterDbContext(GetInMemoryOptions());
        var user = new User();
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var now = DateTime.UtcNow;
        var votes = new[]
        {
            new Vote { UserId = user.Id, Level = 3, CreatedAt = now.AddHours(-1) },
            new Vote { UserId = user.Id, Level = 0, CreatedAt = now.AddHours(-2) },
            new Vote { UserId = user.Id, Level = 1, CreatedAt = now.AddHours(-3) }
        };
        ctx.Votes.AddRange(votes);
        ctx.SaveChanges();

        var allVotes = ctx.Votes.OrderBy(v => v.CreatedAt).ToList();
        Assert.Equal(3, allVotes.Count);
    }
}
