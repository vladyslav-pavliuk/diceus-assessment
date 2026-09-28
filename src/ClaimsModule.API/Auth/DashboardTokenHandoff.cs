using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClaimsModule.API.Auth;

/// <summary>
/// Gets a manager's JWT to the Hangfire dashboard (D-41). The dashboard is opened by browser navigation, which
/// cannot send an Authorization header, and its pages then poll the server on their own. So, only under
/// <see cref="Path"/>:
/// <list type="number">
/// <item>the first visit may carry the token as <c>?access_token=</c> (the same hand-off ASP.NET Core documents
/// for SignalR);</item>
/// <item>once that token is valid, it is kept in an HttpOnly, Secure, SameSite=Strict cookie scoped to the
/// dashboard path and expiring with the token, which later dashboard requests present instead.</item>
/// </list>
/// Everywhere else only the Authorization header counts. Authorization (the Manager policy) is unchanged; this
/// only decides where the token is read from. Request logging records the path without the query string.
/// </summary>
internal static class DashboardTokenHandoff
{
    public const string Path = "/hangfire";
    public const string CookieName = "claims-dashboard-token";
    public const string QueryParameter = "access_token";

    private const string TokenFromQueryItem = nameof(DashboardTokenHandoff);

    public static Task OnMessageReceived(MessageReceivedContext context)
    {
        var request = context.Request;
        if (!request.Path.StartsWithSegments(Path) || request.Headers.Authorization.Count > 0)
        {
            return Task.CompletedTask;
        }

        if (request.Query.TryGetValue(QueryParameter, out var queryToken) && !string.IsNullOrEmpty(queryToken))
        {
            context.Token = queryToken;
            context.HttpContext.Items[TokenFromQueryItem] = true;
        }
        else if (request.Cookies.TryGetValue(CookieName, out var cookieToken))
        {
            context.Token = cookieToken;
        }

        return Task.CompletedTask;
    }

    public static Task OnTokenValidated(TokenValidatedContext context)
    {
        if (context.HttpContext.Items.ContainsKey(TokenFromQueryItem) && context.SecurityToken is JsonWebToken token)
        {
            context.Response.Cookies.Append(CookieName, token.EncodedToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = Path,
                Expires = token.ValidTo,
            });
        }

        return Task.CompletedTask;
    }
}
