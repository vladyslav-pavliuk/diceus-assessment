using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Documents;

/// <summary>
/// The FRS §13 allowlist (D-42). The extension decides the format; the declared Content-Type may confirm it
/// or be non-committal but never contradict it. The bytes are sniffed in the Application layer, and only the
/// canonical <see cref="ContentType"/> is ever stored or served.
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

    /// <summary>Browsers on Windows with Excel installed declare .csv as <c>application/vnd.ms-excel</c>.</summary>
    public static readonly DocumentFormat Csv = new(
        "CSV", "text/csv", [".csv"], ["application/csv", "text/x-csv", "text/comma-separated-values", "application/vnd.ms-excel"], displayInline: false);

    public static readonly IReadOnlyList<DocumentFormat> All = [Pdf, Jpeg, Png, Docx, Xlsx, Txt, Csv];

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

    public string ContentType { get; }

    /// <summary>Only PDF and images open inline, so a text file containing HTML is never rendered.</summary>
    public bool DisplayInline { get; }

    public static DocumentFormat? FromExtension(string extension) =>
        All.FirstOrDefault(format => format._extensions.Contains(extension));

    public static DocumentFormat? FromContentType(string contentType) =>
        All.FirstOrDefault(format => string.Equals(format.ContentType, contentType, StringComparison.OrdinalIgnoreCase));

    /// <summary>Throws a 422 when the extension is not allowlisted or the declared type contradicts it.</summary>
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
