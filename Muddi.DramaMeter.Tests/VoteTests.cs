using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Tests;

public class VoteTests
{
    [Fact]
    public void Vote_CreatedWithDefaults_SetsDefaults()
    {
        var user = new User();
        var vote = new Vote { UserId = user.Id };

        Assert.Equal(user.Id, vote.UserId);
        Assert.Equal(0, vote.Level); // default int is 0 = No Drama
        Assert.True(vote.CreatedAt <= DateTime.UtcNow);
        Assert.Null(vote.User); // FK navigation not set by default
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Vote_CreatedWithValidLevel_SetsLevelCorrectly(int level)
    {
        var user = new User();
        var vote = new Vote { UserId = user.Id, Level = level };

        Assert.Equal(level, vote.Level);
    }

    [Fact]
    public void Vote_UserRelation_CanNavigate()
    {
        var user = new User();
        var vote = new Vote { UserId = user.Id, User = user };

        Assert.Same(user, vote.User);
    }

    [Fact]
    public void Vote_Id_IsAutoIncrementLong()
    {
        var vote = new Vote();

        Assert.Equal(0, vote.Id); // default long is 0
    }
}
