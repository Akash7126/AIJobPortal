using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application;

/// <summary>Renders a tabular report to CSV, a minimal valid Excel (OOXML) workbook or a minimal valid PDF, with no third-party library (THR-081).</summary>
public sealed class ReportFileGenerator : IReportFileGenerator
{
    public GeneratedFile Generate(string title, ReportTable table, ReportFormat format)
    {
        var safe = new string(title.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        safe = string.IsNullOrEmpty(safe) ? "report" : safe;
        return format switch
        {
            ReportFormat.Csv => new GeneratedFile(safe + ".csv", "text/csv", Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Csv(table))).ToArray()),
            ReportFormat.Excel => new GeneratedFile(safe + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Xlsx(title, table)),
            _ => new GeneratedFile(safe + ".pdf", "application/pdf", Pdf(title, table))
        };
    }

    public static string Format(object? value) => value switch
    {
        null => string.Empty,
        decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Csv(ReportTable table)
    {
        static string Cell(string text) => text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        var sb = new StringBuilder();
        sb.Append(string.Join(',', table.Columns.Select(c => Cell(c.Name)))).Append("\r\n");
        foreach (var row in table.Rows)
        {
            sb.Append(string.Join(',', row.Select(v => Cell(Format(v))))).Append("\r\n");
        }

        return sb.ToString();
    }

    private static byte[] Xlsx(string title, ReportTable table)
    {
        static string Col(int i) => ((char)('A' + i % 26)).ToString();
        var sheet = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        var rowNumber = 1;
        void Row(IEnumerable<object?> values)
        {
            sheet.Append("<row r=\"").Append(rowNumber).Append("\">");
            var i = 0;
            foreach (var v in values)
            {
                var reference = Col(i++) + rowNumber;
                if (v is decimal or long or int or double)
                {
                    sheet.Append("<c r=\"").Append(reference).Append("\"><v>").Append(Convert.ToString(v, CultureInfo.InvariantCulture)).Append("</v></c>");
                }
                else
                {
                    sheet.Append("<c r=\"").Append(reference).Append("\" t=\"inlineStr\"><is><t>").Append(SecurityElement.Escape(Format(v))).Append("</t></is></c>");
                }
            }

            sheet.Append("</row>");
            rowNumber++;
        }

        Row(table.Columns.Select(c => (object?)c.Name));
        foreach (var row in table.Rows)
        {
            Row(row);
        }

        sheet.Append("</sheetData></worksheet>");

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            Entry("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Entry("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Entry("xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Report\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Entry("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Entry("xl/worksheets/sheet1.xml", sheet.ToString());
        }

        _ = title;
        return stream.ToArray();
    }

    private static byte[] Pdf(string title, ReportTable table)
    {
        static string Esc(string s) => new string(s.Select(c => c < 32 || c > 126 ? '?' : c).ToArray()).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var lines = new List<string> { title, string.Empty, string.Join(" | ", table.Columns.Select(c => c.Name)) };
        lines.AddRange(table.Rows.Take(45).Select(r => string.Join(" | ", r.Select(Format))));
        if (table.Rows.Count > 45)
        {
            lines.Add($"... {table.Rows.Count - 45} more rows (see the CSV or Excel export)");
        }

        var content = new StringBuilder("BT /F1 10 Tf 40 800 Td 13 TL\n");
        foreach (var line in lines)
        {
            content.Append('(').Append(Esc(line.Length > 110 ? line[..110] : line)).Append(") Tj T*\n");
        }

        content.Append("ET");
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        pdf.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
