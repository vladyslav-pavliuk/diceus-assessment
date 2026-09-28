using System.IO.Compression;
using System.Text;
using ClaimsModule.Application.Claims.Documents;
using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Application.Tests.Documents;

/// <summary>DOC-05: the allowlist is checked by content sniffing, not by the Content-Type header or the extension alone (FRS §13, D-42).</summary>
public sealed class DocumentContentInspectorTests
{
    public static TheoryData<string, byte[]> GenuineFiles => new()
    {
        { "PDF", Encoding.ASCII.GetBytes("%PDF-1.7\n%âã\n1 0 obj\n<<>>\nendobj\n") },
        { "JPEG", [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46] },
        { "PNG", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D] },
        { "DOCX", Zip("[Content_Types].xml", "_rels/.rels", "word/document.xml") },
        { "XLSX", Zip("[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/worksheets/sheet1.xml") },
        { "TXT", Encoding.UTF8.GetBytes("Adjuster notes\r\n\tCafé visit — 3 photos.\f\n") },
        { "CSV", Encoding.Latin1.GetBytes("item;amount\ncafé;12,50\n") }, // a single-byte encoding passes too
    };

    [Theory]
    [MemberData(nameof(GenuineFiles))]
    public void DOC_05_Genuine_files_match_their_format(string format, byte[] content)
    {
        DocumentContentInspector.Matches(Format(format), new MemoryStream(content)).ShouldBeTrue();
    }

    public static TheoryData<string, string, byte[]> SpoofedFiles => new()
    {
        { "Windows executable renamed .pdf", "PDF", [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00] },
        { "HTML renamed .pdf", "PDF", Encoding.ASCII.GetBytes("<html><script>alert(1)</script></html>") },
        { "PNG renamed .jpg", "JPEG", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A] },
        { "JPEG renamed .png", "PNG", [0xFF, 0xD8, 0xFF, 0xE0] },
        { "truncated PNG signature", "PNG", [0x89, 0x50, 0x4E] },
        { "empty file", "PDF", [] },
        { "XLSX renamed .docx", "DOCX", Zip("[Content_Types].xml", "xl/workbook.xml") },
        { "DOCX renamed .xlsx", "XLSX", Zip("[Content_Types].xml", "word/document.xml") },
        { "plain ZIP renamed .docx", "DOCX", Zip("payload.exe") },
        { "macro-enabled .docm renamed .docx", "DOCX", Zip("[Content_Types].xml", "word/document.xml", "word/vbaProject.bin") },
        { "macro-enabled .xlsm renamed .xlsx", "XLSX", Zip("[Content_Types].xml", "xl/workbook.xml", "xl/vbaProject.bin") },
        { "ZIP header on garbage", "XLSX", [0x50, 0x4B, 0x03, 0x04, 0x01, 0x02, 0x03] },
        { "binary with NUL renamed .txt", "TXT", [0x68, 0x69, 0x00, 0x21] },
        { "UTF-16 text renamed .csv", "CSV", Encoding.Unicode.GetBytes("a,b\n1,2\n") },
        { "PNG renamed .txt", "TXT", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00] },
        { "ANSI escape sequence in .txt", "TXT", Encoding.ASCII.GetBytes("ok\u001b[31mred") },
    };

    [Theory]
    [MemberData(nameof(SpoofedFiles))]
    public void DOC_05_Spoofed_content_is_rejected(string description, string format, byte[] content)
    {
        DocumentContentInspector.Matches(Format(format), new MemoryStream(content)).ShouldBeFalse(description);
    }

    /// <summary>A binary byte after the first 80 KB buffer still counts: text is checked end to end.</summary>
    [Fact]
    public void DOC_05_Text_is_checked_to_the_last_byte()
    {
        var content = Enumerable.Repeat((byte)'a', 200_000).Append((byte)0x00).ToArray();

        DocumentContentInspector.Matches(DocumentFormat.Txt, new MemoryStream(content)).ShouldBeFalse();
    }

    /// <summary>
    /// Text is only "no binary bytes": an HTML page named .txt passes. It is harmless because it is stored and served as
    /// text/plain, as an attachment (<see cref="DocumentFormat.DisplayInline"/> is false for text).
    /// </summary>
    [Fact]
    public void DOC_05_Html_named_txt_is_text_and_is_served_as_a_text_attachment()
    {
        var html = new MemoryStream(Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"));

        DocumentContentInspector.Matches(DocumentFormat.Txt, html).ShouldBeTrue();
        DocumentFormat.Txt.ContentType.ShouldBe("text/plain");
        DocumentFormat.Txt.DisplayInline.ShouldBeFalse();
    }

    [Fact]
    public void DOC_05_The_stream_is_rewound_for_the_upload()
    {
        var content = new MemoryStream(Zip("[Content_Types].xml", "word/document.xml")) { Position = 7 };

        DocumentContentInspector.Matches(DocumentFormat.Docx, content).ShouldBeTrue();
        content.Position.ShouldBe(0);
    }

    private static DocumentFormat Format(string name) => DocumentFormat.All.Single(format => format.Name == name);

    private static byte[] Zip(params string[] entries)
    {
        var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
                writer.Write("<xml/>");
            }
        }

        return buffer.ToArray();
    }
}
