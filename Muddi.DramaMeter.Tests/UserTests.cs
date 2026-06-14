using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Tests;

public class UserTests
{
	[Fact]
	public void User_CreatedWithDefaults_GuidIdAndUtcTimestamp()
	{
		var user = new User();

		user.Id.Should().NotBe(Guid.Empty);
		user.CreatedAt.Should().BeBefore(DateTime.UtcNow.AddMilliseconds(1));
	}

	[Fact]
	public void User_CreatedWithCustomId_SetsIdCorrectly()
	{
		var id = Guid.NewGuid();
		var user = new User { Id = id };

		user.Id.Should().Be(id);
	}

	[Fact]
	public void User_CreatedWithCustomCreatedAt_SetsCorrectly()
	{
		var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
		var user = new User { CreatedAt = now };

		user.CreatedAt.Should().Be(now);
	}
}