using System.IO.Compression;
using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Application.Claims.Documents;

/// <summary>
/// Checks that the bytes really are the format the extension claims (D-42), since the declared Content-Type proves nothing.
/// <list type="bullet">
/// <item>PDF, JPEG, PNG: the file signature.</item>
/// <item>DOCX, XLSX: the ZIP directory has the format's main part and no <c>vbaProject.bin</c>, so a renamed macro-enabled
/// file is refused. Nothing is decompressed.</item>
/// <item>TXT, CSV: no binary control characters anywhere, so UTF-16 is refused.</item>
/// </list>
/// The stream is left at position 0.
/// </summary>
internal static class DocumentContentInspector
{
    public const string ContentMismatchMessage = "The file content does not match its type.";

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    public static bool Matches(DocumentFormat format, Stream content)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanSeek)
        {
            throw new ArgumentException("Content sniffing needs a seekable stream.", nameof(content));
        }

        try
        {
            content.Position = 0;
            return Check(format, content);
        }
        finally
        {
            content.Position = 0;
        }
    }

    private static bool Check(DocumentFormat format, Stream content)
    {
        if (format == DocumentFormat.Pdf)
        {
            return StartsWith(content, PdfSignature);
        }

        if (format == DocumentFormat.Jpeg)
        {
            return StartsWith(content, JpegSignature);
        }

        if (format == DocumentFormat.Png)
        {
            return StartsWith(content, PngSignature);
        }

        if (format == DocumentFormat.Docx)
        {
            return IsOfficeOpenXml(content, "word/document.xml");
        }

        if (format == DocumentFormat.Xlsx)
        {
            return IsOfficeOpenXml(content, "xl/workbook.xml");
        }

        return (format == DocumentFormat.Txt || format == DocumentFormat.Csv) && IsText(content);
    }

    private static bool StartsWith(Stream content, byte[] signature)
    {
        Span<byte> header = stackalloc byte[signature.Length];
        return content.ReadAtLeast(header, signature.Length, throwOnEndOfStream: false) == signature.Length
            && header.SequenceEqual(signature);
    }

    private static bool IsOfficeOpenXml(Stream content, string mainPart)
    {
        if (!StartsWith(content, ZipSignature))
        {
            return false;
        }

        content.Position = 0;
        try
        {
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            var names = zip.Entries.Select(entry => entry.FullName).ToList();

            return names.Contains("[Content_Types].xml", StringComparer.OrdinalIgnoreCase)
                && names.Contains(mainPart, StringComparer.OrdinalIgnoreCase)
                && !names.Any(name => name.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException)
        {
            return false; // a ZIP header on something that is not a readable ZIP
        }
    }

    private static bool IsText(Stream content)
    {
        var buffer = new byte[81_920];
        int read;
        while ((read = content.Read(buffer, 0, buffer.Length)) > 0)
        {
            foreach (var value in buffer.AsSpan(0, read))
            {
                if (value < 0x20 && value is not (0x09 or 0x0A or 0x0C or 0x0D))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
