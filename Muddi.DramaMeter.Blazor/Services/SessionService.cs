using Microsoft.EntityFrameworkCore;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Blazor.Services;

public interface ISessionService
{
	/// <summary>
	///     Gets the current user's session, creating it if necessary.
	/// </summary>
	Task<User> GetOrCreateUserAsync();
}

public class SessionService(
	IHttpContextAccessor httpContextAccessor,
	IDbContextFactory<DramaMeterDbContext> contextFactory) : ISessionService
{
	private const string CookieName = "drama_meter_session";
	private const int CookieDays = 365;
	private readonly SemaphoreSlim _semaphore = new(1, 1);
	private User? _cachedUser;

	public async Task<User> GetOrCreateUserAsync()
	{
		if (_cachedUser is not null) 
			return _cachedUser;

		await _semaphore.WaitAsync();
		try
		{
			if (_cachedUser is not null) 
				return _cachedUser;

			var userId = GetSessionCookie();

			await using var dbContext = await contextFactory.CreateDbContextAsync();

			User? user = null;
			if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var guid))
				user = await dbContext.Users.FindAsync(guid);

			if (user is null)
			{
				user = new User();
				dbContext.Users.Add(user);
				await dbContext.SaveChangesAsync();
				// Session cookie is set by SessionCreationMiddleware on the initial GET
			}

			_cachedUser = user;
			return user;
		}
		finally
		{
			_semaphore.Release();
		}
	}

	private string? GetSessionCookie()
	{
		return httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
	}
}