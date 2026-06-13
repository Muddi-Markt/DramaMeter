using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Tests;

public class UserTests
{
    [Fact]
    public void User_CreatedWithDefaults_GuidIdAndUtcTimestamp()
    {
        var user = new User();

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.True(user.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void User_CreatedWithCustomId_SetsIdCorrectly()
    {
        var id = Guid.NewGuid();
        var user = new User { Id = id };

        Assert.Equal(id, user.Id);
    }

    [Fact]
    public void User_CreatedWithCustomCreatedAt_SetsCorrectly()
    {
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User { CreatedAt = now };

        Assert.Equal(now, user.CreatedAt);
    }
}
