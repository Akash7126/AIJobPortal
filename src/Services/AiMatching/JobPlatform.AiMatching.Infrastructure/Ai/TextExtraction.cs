using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace JobPlatform.AiMatching.Infrastructure.Ai;

/// <summary>Best-effort text extraction from the three accepted resume formats. Returns null when the file is corrupt or holds no readable text (INV-08).</summary>
public static partial class TextExtraction
{
    public static string? Extract(byte[] content, string format) => format.Trim().ToUpperInvariant() switch
    {
        "TXT" => FromText(content),
        "DOCX" => FromDocx(content),
        "PDF" => FromPdf(content),
        _ => null
    };

    private static string? FromText(byte[] content)
    {
        if (content.Length == 0 || content.Contains((byte)0))
        {
            return null; // binary data is not a text resume
        }

        var text = new UTF8Encoding(false, true).TryGetString(content) ?? Encoding.Latin1.GetString(content);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? FromDocx(byte[] content)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null)
            {
                return null;
            }

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var xml = reader.ReadToEnd();
            var text = Regex.Replace(Regex.Replace(xml, "</w:p>", "\n"), "<[^>]+>", string.Empty);
            text = System.Net.WebUtility.HtmlDecode(text);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Reads the text-showing operators (Tj/TJ) of uncompressed or Flate-compressed content streams. Simple PDFs only; scanned or exotic files yield null.</summary>
    private static string? FromPdf(byte[] content)
    {
        if (content.Length < 8 || Encoding.ASCII.GetString(content, 0, 5) != "%PDF-")
        {
            return null;
        }

        var raw = Encoding.Latin1.GetString(content);
        var builder = new StringBuilder();
        foreach (Match stream in PdfStream().Matches(raw))
        {
            var bytes = Encoding.Latin1.GetBytes(stream.Groups[1].Value);
            var text = TryInflate(bytes) ?? stream.Groups[1].Value;
            foreach (Match show in PdfText().Matches(text))
            {
                builder.Append(show.Groups[1].Success ? Unescape(show.Groups[1].Value) : string.Concat(PdfString().Matches(show.Groups[2].Value).Select(m => Unescape(m.Groups[1].Value)))).Append(" ");
            }

            builder.AppendLine();
        }

        var result = builder.ToString();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string? TryInflate(byte[] data)
    {
        try
        {
            using var zlib = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return Encoding.Latin1.GetString(output.ToArray());
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return null;
        }
    }

    private static string Unescape(string s) => s.Replace("\\(", "(").Replace("\\)", ")").Replace("\\\\", "\\").Replace("\\n", "\n");

    private static string? TryGetString(this UTF8Encoding encoding, byte[] bytes)
    {
        try
        {
            return encoding.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"\(((?:\\.|[^\\)])*)\)")]
    private static partial Regex PdfString();

    [GeneratedRegex(@"stream\r?\n(.*?)\r?\nendstream", RegexOptions.Singleline)]
    private static partial Regex PdfStream();

    [GeneratedRegex(@"\(((?:\\.|[^\\)])*)\)\s*Tj|\[((?:\((?:\\.|[^\\)])*\)|[^\]])*)\]\s*TJ")]
    private static partial Regex PdfText();
}
