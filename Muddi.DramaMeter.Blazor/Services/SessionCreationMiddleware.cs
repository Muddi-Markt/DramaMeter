using Microsoft.EntityFrameworkCore;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Blazor.Services;

public class SessionCreationMiddleware
{
	private const string CookieName = "drama_meter_session";
	private const int CookieDays = 365;

	private readonly RequestDelegate _next;
	private readonly IDbContextFactory<DramaMeterDbContext> _contextFactory;

	public SessionCreationMiddleware(
		RequestDelegate next,
		IDbContextFactory<DramaMeterDbContext> contextFactory)
	{
		_next = next;
		_contextFactory = contextFactory;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		// Only handle initial GET requests (not SignalR, not API calls)
		if (!HttpMethods.IsGet(context.Request.Method) || context.Request.Path.StartsWithSegments("/_blazor"))
		{
			await _next(context);
			return;
		}

		// If session cookie already exists, nothing to do
		if (!string.IsNullOrEmpty(context.Request.Cookies[CookieName]))
		{
			await _next(context);
			return;
		}

		// Create a new user and set the session cookie before the response starts
		await using var dbContext = await _contextFactory.CreateDbContextAsync();
		var user = new User();
		dbContext.Users.Add(user);
		await dbContext.SaveChangesAsync();

		context.Response.Cookies.Append(
			CookieName,
			user.Id.ToString(),
			new CookieOptions
			{
				HttpOnly = true,
				SameSite = SameSiteMode.Lax,
				Expires = DateTime.UtcNow.AddDays(CookieDays),
				Path = "/"
			});

		await _next(context);
	}
}
