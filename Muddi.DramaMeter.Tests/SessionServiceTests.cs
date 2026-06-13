using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;
using Muddi.DramaMeter.Blazor.Services;

namespace Muddi.DramaMeter.Tests;

internal sealed class TestCookieCollection : IRequestCookieCollection
{
    private readonly Dictionary<string, string> _cookies;

    public TestCookieCollection(Dictionary<string, string> cookies)
    {
        _cookies = cookies;
    }

    public string? this[string key] => _cookies.TryGetValue(key, out var value) ? value : null;
    public int Count => _cookies.Count;
    public ICollection<string> Keys => _cookies.Keys;
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _cookies.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _cookies.GetEnumerator();
    public bool ContainsKey(string key) => _cookies.ContainsKey(key);
    public bool TryGetValue(string key, out string? value) => _cookies.TryGetValue(key, out value);
    public ICollection<string> Values => _cookies.Values;
}

public class SessionServiceTests
{
    private DbContextOptions<DramaMeterDbContext> GetInMemoryOptions()
    {
        var builder = new DbContextOptionsBuilder<DramaMeterDbContext>();
        builder.UseInMemoryDatabase($"SessionTest_{Guid.NewGuid():N}");
        return builder.Options;
    }

    [Fact]
    public async Task GetOrCreateUserAsync_NoCookie_CreatesNewUser()
    {
        // Arrange
        var context = CreateDbContext();
        var accessor = CreateHttpContextAccessor(cookieValue: null);

        // Act
        var service = new SessionService(accessor, context);
        var user = await service.GetOrCreateUserAsync();

        // Assert
        user.Id.Should().NotBe(Guid.Empty);

        // Verify cookie was set
        accessor.HttpContext.Response.Cookies
            .Received(1).Append("drama_meter_session", user.Id.ToString(), Arg.Is<CookieOptions>(o =>
                o.HttpOnly && o.SameSite == SameSiteMode.Lax && o.Path == "/" && o.Expires.Value.Year > 2026));
    }

    [Fact]
    public async Task GetOrCreateUserAsync_ExistingCookie_ReturnsSameUser()
    {
        // Arrange
        var context = CreateDbContext();
        var existingUser = new User { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        var accessor = CreateHttpContextAccessor(cookieValue: "11111111-1111-1111-1111-111111111111");

        // Act
        var service = new SessionService(accessor, context);
        var user = await service.GetOrCreateUserAsync();

        // Assert
        user.Id.Should().Be(existingUser.Id);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_NewUser_SetsCookieWithCorrectId()
    {
        // Arrange
        var context = CreateDbContext();
        var accessor = CreateHttpContextAccessor(cookieValue: null);

        // Act
        var service = new SessionService(accessor, context);
        var user = await service.GetOrCreateUserAsync();

        // Assert
        accessor.HttpContext.Response.Cookies
            .Received(1).Append("drama_meter_session", user.Id.ToString(), Arg.Any<CookieOptions>());
    }

    [Fact]
    public void GetSessionCookie_NoCookie_ReturnsNull()
    {
        // Arrange
        var context = CreateDbContext();
        var accessor = CreateHttpContextAccessor(cookieValue: null);

        // Act
        var service = new SessionService(accessor, context);
        var result = service.GetSessionCookie();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetSessionCookie_ExistingCookie_ReturnsValue()
    {
        // Arrange
        var context = CreateDbContext();
        var expectedId = "11111111-2222-3333-4444-555566667777";
        var accessor = CreateHttpContextAccessor(cookieValue: expectedId);

        // Act
        var service = new SessionService(accessor, context);
        var result = service.GetSessionCookie();

        // Assert
        result.Should().Be(expectedId);
    }

    private DramaMeterDbContext CreateDbContext()
    {
        var options = GetInMemoryOptions();
        return new DramaMeterDbContext(options);
    }

    private IHttpContextAccessor CreateHttpContextAccessor(string? cookieValue)
    {
        var mockHttpContext = Substitute.For<HttpContext>();
        var mockServiceProvider = Substitute.For<IServiceProvider>();

        // Setup Request.Cookies
        var cookieDict = cookieValue is not null
            ? new Dictionary<string, string> { { "drama_meter_session", cookieValue } }
            : new Dictionary<string, string>();
        var mockRequest = Substitute.For<HttpRequest>();
        mockRequest.Cookies.Returns(new TestCookieCollection(cookieDict));

        // Setup Response.Cookies
        var mockResponse = Substitute.For<HttpResponse>();
        var mockCookies = Substitute.For<IResponseCookies>();
        mockResponse.Cookies.Returns(mockCookies);

        mockHttpContext.Request.Returns(mockRequest);
        mockHttpContext.Response.Returns(mockResponse);
        mockHttpContext.RequestServices.Returns(mockServiceProvider);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(mockHttpContext);

        return accessor;
    }
}
