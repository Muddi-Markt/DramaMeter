using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Tests;

public class VoteTests
{
    [Fact]
    public void Vote_CreatedWithDefaults_SetsDefaults()
    {
        var user = new User();
        var vote = new Vote();

        vote.User.Should().BeNull(); // FK navigation not set by default
        vote.Level.Should().Be(0); // default int is 0 = No Drama
        vote.CreatedAt.Should().BeBefore(DateTimeOffset.UtcNow.AddMilliseconds(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Vote_CreatedWithValidLevel_SetsLevelCorrectly(int level)
    {
        var user = new User();
        var vote = new Vote { User = user, Level = level };

        vote.Level.Should().Be(level);
    }

    [Fact]
    public void Vote_UserRelation_CanNavigate()
    {
        var user = new User();
        var vote = new Vote { User = user };

        vote.User.Should().Be(user);
    }

    [Fact]
    public void Vote_Id_IsAutoIncrementLong()
    {
        var vote = new Vote();

        vote.Id.Should().Be(0); // default long is 0
    }
}
