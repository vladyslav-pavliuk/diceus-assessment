using ClaimsModule.Application.Abstractions;

namespace ClaimsModule.Infrastructure.Storage;

internal static class ContentDispositions
{
    /// <summary>
    /// <c>inline|attachment; filename="ascii fallback"; filename*=UTF-8''percent-encoded</c> (RFC 6266). Browsers use
    /// filename*, so a Unicode name survives; the quoted ASCII fallback has quotes and backslashes replaced, so the name
    /// can never break out of the header value.
    /// </summary>
    public static string Format(DownloadHeaders headers)
    {
        var fallback = new string(headers.FileName.Select(character => character is < ' ' or > '~' or '"' or '\\' ? '_' : character).ToArray());
        return $"{(headers.Inline ? "inline" : "attachment")}; filename=\"{fallback}\"; filename*=UTF-8''{Uri.EscapeDataString(headers.FileName)}";
    }
}
