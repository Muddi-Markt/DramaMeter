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

	public async Task<User> GetOrCreateUserAsync()
	{
		await _semaphore.WaitAsync();
		try
		{
			var userId = GetSessionCookie();

			await using var dbContext = await contextFactory.CreateDbContextAsync();

			User? user = null;
			if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var guid))
				user = await dbContext.Users.FindAsync(guid);

			if (user is not null)
				return user;

			user = new User();
			dbContext.Users.Add(user);
			await dbContext.SaveChangesAsync();

			SetSessionCookie(user.Id);

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

	private void SetSessionCookie(Guid userId)
	{
		var cookieOptions = new CookieOptions
		{
			HttpOnly = true,
			SameSite = SameSiteMode.Lax,
			Expires = DateTime.UtcNow.AddDays(CookieDays),
			Path = "/"
		};

		var context = httpContextAccessor.HttpContext;
		if (context is not null) context.Response.Cookies.Append(CookieName, userId.ToString(), cookieOptions);
	}
}