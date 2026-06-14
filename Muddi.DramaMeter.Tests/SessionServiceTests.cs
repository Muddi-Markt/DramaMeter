using System.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;
using Muddi.DramaMeter.Blazor.Services;
using NSubstitute;

namespace Muddi.DramaMeter.Tests;

internal sealed class TestCookieCollection : IRequestCookieCollection
{
	private readonly Dictionary<string, string> _cookies;

	public TestCookieCollection(Dictionary<string, string> cookies)
	{
		_cookies = cookies;
	}

	public ICollection<string> Values => _cookies.Values;

	public string? this[string key] => _cookies.GetValueOrDefault(key);
	public int Count => _cookies.Count;
	public ICollection<string> Keys => _cookies.Keys;

	public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
	{
		return _cookies.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return _cookies.GetEnumerator();
	}

	public bool ContainsKey(string key)
	{
		return _cookies.ContainsKey(key);
	}

	public bool TryGetValue(string key, out string value)
	{
		return _cookies.TryGetValue(key, out value!);
	}
}

public class SessionServiceTests
{
	private DbContextOptions<DramaMeterDbContext> GetInMemoryOptions()
	{
		var builder = new DbContextOptionsBuilder<DramaMeterDbContext>();
		builder.UseInMemoryDatabase($"SessionTest_{Guid.NewGuid():N}");
		return builder.Options;
	}

	private IDbContextFactory<DramaMeterDbContext> CreateDbContextFactory()
	{
		var options = GetInMemoryOptions();
		return new TestDbContextFactory(options);
	}

	[Fact]
	public async Task GetOrCreateUserAsync_NoCookie_CreatesNewUser()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var accessor = CreateHttpContextAccessor(null);

		// Act
		var service = new SessionService(accessor, factory);
		var user = await service.GetOrCreateUserAsync();

		// Assert
		user.Id.Should().NotBe(Guid.Empty);

		// Verify cookie was set
		accessor.HttpContext!.Response.Cookies
			.Received(1).Append("drama_meter_session", user.Id.ToString(), Arg.Is<CookieOptions>(o =>
				o.HttpOnly && o.SameSite == SameSiteMode.Lax && o.Path == "/" && o.Expires!.Value.Year > 2026));
	}

	[Fact]
	public async Task GetOrCreateUserAsync_ExistingCookie_ReturnsSameUser()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var existingUser = new User { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") };
		var db = factory.CreateDbContext();
		db.Users.Add(existingUser);
		await db.SaveChangesAsync();

		var accessor = CreateHttpContextAccessor("11111111-1111-1111-1111-111111111111");

		// Act
		var service = new SessionService(accessor, factory);
		var user = await service.GetOrCreateUserAsync();

		// Assert
		user.Id.Should().Be(existingUser.Id);
	}

	[Fact]
	public async Task GetOrCreateUserAsync_NewUser_SetsCookieWithCorrectId()
	{
		// Arrange
		var factory = CreateDbContextFactory();
		var accessor = CreateHttpContextAccessor(null);

		// Act
		var service = new SessionService(accessor, factory);
		var user = await service.GetOrCreateUserAsync();

		// Assert
		accessor.HttpContext!.Response.Cookies
			.Received(1).Append("drama_meter_session", user.Id.ToString(), Arg.Any<CookieOptions>());
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

	private sealed class TestDbContextFactory(DbContextOptions<DramaMeterDbContext> options) : IDbContextFactory<DramaMeterDbContext>
	{
		public DramaMeterDbContext CreateDbContext() => new DramaMeterDbContext(options);

		public IDisposable? BeginTransaction() => throw new NotImplementedException();

		public IQueryable<TElement> CreateAsyncQueryExecutor<TElement>() => throw new NotImplementedException();

		public IAsyncEnumerator<TElement> CreateAsyncEnumerator<TElement>(IQueryable<TElement> query) => throw new NotImplementedException();

		public IQueryProvider CreateQueryProvider() => throw new NotImplementedException();
	}
}
