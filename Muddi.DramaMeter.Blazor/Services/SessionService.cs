using Microsoft.AspNetCore.Http;
using Muddi.DramaMeter.Blazor.Data;
using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Blazor.Services;

public interface ISessionService
{
    /// <summary>
    /// Gets the current user's session, creating it if necessary.
    /// </summary>
    Task<User> GetOrCreateUserAsync();

    /// <summary>
    /// Gets the current user's ID as a string from the cookie.
    /// Returns null if no valid session cookie exists.
    /// </summary>
    string? GetSessionCookie();
}

public class SessionService(
    IHttpContextAccessor httpContextAccessor,
    DramaMeterDbContext dbContext) : ISessionService
{
    private const string CookieName = "drama_meter_session";
    private const int CookieDays = 365;

    public string? GetSessionCookie()
    {
        return httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
    }

    public async Task<User> GetOrCreateUserAsync()
    {
        var userId = GetSessionCookie();

        User? user = null;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var guid))
        {
            user = await dbContext.Users.FindAsync(guid);
        }

        if (user is null)
        {
            user = new User();
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            SetSessionCookie(user.Id);
        }

        return user;
    }

    private void SetSessionCookie(Guid userId)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(CookieDays),
            Path = "/",
        };

        var context = httpContextAccessor.HttpContext;
        if (context is not null)
        {
            context.Response.Cookies.Append(CookieName, userId.ToString(), cookieOptions);
        }
    }
}
