using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Documents;

/// <summary>
/// The MIME allowlist of FRS §13: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV (D-42).
/// <para>
/// A file's format is claimed by its <b>extension</b> (what the user and whoever downloads it will see). The declared
/// Content-Type may confirm the claim or say nothing (empty, <c>application/octet-stream</c>), but it may not contradict
/// it. Whether the <b>bytes</b> really are that format is checked by content sniffing in the Application layer before
/// anything is stored. The stored and served content type is always <see cref="ContentType"/>, never the client's string.
/// </para>
/// </summary>
public sealed record DocumentFormat
{
    public const string NotAllowedMessage = "File type is not allowed. Allowed types: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV.";
    public const string DeclaredTypeMismatchMessage = "The declared content type does not match the file extension.";

    public static readonly DocumentFormat Pdf = new(
        "PDF", "application/pdf", [".pdf"], ["application/x-pdf"], displayInline: true);

    public static readonly DocumentFormat Jpeg = new(
        "JPEG", "image/jpeg", [".jpg", ".jpeg"], ["image/jpg", "image/pjpeg"], displayInline: true);

    public static readonly DocumentFormat Png = new(
        "PNG", "image/png", [".png"], ["image/x-png"], displayInline: true);

    public static readonly DocumentFormat Docx = new(
        "DOCX", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [".docx"], [], displayInline: false);

    public static readonly DocumentFormat Xlsx = new(
        "XLSX", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [".xlsx"], [], displayInline: false);

    public static readonly DocumentFormat Txt = new(
        "TXT", "text/plain", [".txt"], [], displayInline: false);

    /// <summary>Windows browsers declare .csv as <c>application/vnd.ms-excel</c> when Excel is installed, so that alias is accepted.</summary>
    public static readonly DocumentFormat Csv = new(
        "CSV", "text/csv", [".csv"], ["application/csv", "text/x-csv", "text/comma-separated-values", "application/vnd.ms-excel"], displayInline: false);

    public static readonly IReadOnlyList<DocumentFormat> All = [Pdf, Jpeg, Png, Docx, Xlsx, Txt, Csv];

    /// <summary>What a browser declares when it does not know the type; it neither confirms nor contradicts the extension.</summary>
    private static readonly HashSet<string> NonCommittalTypes = new(StringComparer.OrdinalIgnoreCase) { "application/octet-stream" };

    private readonly HashSet<string> _extensions;
    private readonly HashSet<string> _acceptedDeclaredTypes;

    private DocumentFormat(string name, string contentType, string[] extensions, string[] aliases, bool displayInline)
    {
        Name = name;
        ContentType = contentType;
        DisplayInline = displayInline;
        _extensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        _acceptedDeclaredTypes = new HashSet<string>([contentType, .. aliases], StringComparer.OrdinalIgnoreCase);
    }

    public string Name { get; }

    /// <summary>The canonical MIME type: stored in ClaimDocuments.ContentType and served on download.</summary>
    public string ContentType { get; }

    /// <summary>
    /// PDF and images open in the browser tab (FRS §11.3 "opens in new tab"); the rest download as attachments, so a text
    /// file that happens to contain HTML is never rendered.
    /// </summary>
    public bool DisplayInline { get; }

    public static DocumentFormat? FromExtension(string extension) =>
        All.FirstOrDefault(format => format._extensions.Contains(extension));

    /// <summary>Maps a stored ContentType back to its format; null for a value this allowlist never stores.</summary>
    public static DocumentFormat? FromContentType(string contentType) =>
        All.FirstOrDefault(format => string.Equals(format.ContentType, contentType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// FRS §13: the format of an upload, from its extension, checked against the declared Content-Type. Throws a 422
    /// (key "File") for an extension outside the allowlist or a declared type that contradicts it.
    /// </summary>
    public static DocumentFormat Resolve(SanitisedFileName fileName, string? declaredContentType)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var format = FromExtension(fileName.Extension)
            ?? throw new BusinessRuleViolationException(ErrorKeys.File, NotAllowedMessage);

        if (!format.AcceptsDeclaredType(declaredContentType))
        {
            throw new BusinessRuleViolationException(ErrorKeys.File, DeclaredTypeMismatchMessage);
        }

        return format;
    }

    public bool AcceptsDeclaredType(string? declaredContentType)
    {
        // Parameters such as "; charset=utf-8" do not change the type.
        var mediaType = (declaredContentType ?? string.Empty).Split(';', 2)[0].Trim();
        return mediaType.Length == 0 || NonCommittalTypes.Contains(mediaType) || _acceptedDeclaredTypes.Contains(mediaType);
    }

    public bool Equals(DocumentFormat? other) => other is not null && ContentType == other.ContentType;

    public override int GetHashCode() => ContentType.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Name;
}
