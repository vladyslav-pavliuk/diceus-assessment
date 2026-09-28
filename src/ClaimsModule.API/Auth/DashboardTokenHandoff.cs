using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClaimsModule.API.Auth;

/// <summary>
/// Browser navigation cannot send an Authorization header, so under <see cref="Path"/> only, the first visit may carry
/// <c>?access_token=</c> (as ASP.NET Core documents for SignalR) and a valid token is then kept in an HttpOnly, Secure,
/// SameSite=Strict cookie scoped to the dashboard. The Manager policy still decides access (D-41).
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
