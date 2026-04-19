using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.Json;

namespace CLARIHR.Application.Features.Reports;

public static class ReportExportFormats
{
    public const string Csv = "csv";
    public const string Xlsx = "xlsx";
    public const string Json = "json";

    public static bool TryNormalize(string format, out string normalizedFormat)
    {
        normalizedFormat = string.IsNullOrWhiteSpace(format)
            ? Xlsx
            : format.Trim().ToLowerInvariant();

        return normalizedFormat is Csv or Xlsx or Json;
    }

    public static string GetContentType(string normalizedFormat) =>
        normalizedFormat switch
        {
            Csv => "text/csv",
            Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Json => "application/json",
            _ => "application/octet-stream"
        };
}

public static class ReportExportFileWriter
{
    public static async Task WriteAsync<TRow>(
        Stream destination,
        IReadOnlyCollection<TRow> rows,
        string normalizedFormat,
        string sheetName,
        CancellationToken cancellationToken)
    {
        if (normalizedFormat == ReportExportFormats.Csv)
        {
            await WriteCsvAsync(destination, rows, cancellationToken);
            return;
        }

        if (normalizedFormat == ReportExportFormats.Json)
        {
            await JsonSerializer.SerializeAsync(destination, rows, cancellationToken: cancellationToken);
            return;
        }

        await WriteXlsxAsync(destination, rows, sheetName, cancellationToken);
    }

    private static async Task WriteCsvAsync<TRow>(
        Stream destination,
        IReadOnlyCollection<TRow> rows,
        CancellationToken cancellationToken)
    {
        var properties = GetExportProperties<TRow>();
        await using var writer = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

        await writer.WriteLineAsync(string.Join(",", properties.Select(static property => EscapeCsv(property.Name))));
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = properties.Select(property => EscapeCsv(FormatValue(property.GetValue(row))));
            await writer.WriteLineAsync(string.Join(",", values));
        }
    }

    private static Task WriteXlsxAsync<TRow>(
        Stream destination,
        IReadOnlyCollection<TRow> rows,
        string sheetName,
        CancellationToken cancellationToken)
    {
        var properties = GetExportProperties<TRow>();
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        WriteEntry(archive, "[Content_Types].xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>");
        WriteEntry(archive, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>");
        WriteEntry(archive, "xl/workbook.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            $"<sheets><sheet name=\"{EscapeXml(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>");
        WriteEntry(archive, "xl/_rels/workbook.xml.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>");
        WriteEntry(archive, "xl/styles.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
            "<borders count=\"1\"><border/></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>");

        var sheetEntry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
        using var sheetStream = sheetEntry.Open();
        using var writer = new StreamWriter(sheetStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        writer.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        writer.Write("<row r=\"1\">");
        foreach (var property in properties)
        {
            writer.Write(Cell(property.Name));
        }

        writer.Write("</row>");

        var rowIndex = 2;
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.Write($"<row r=\"{rowIndex++}\">");
            foreach (var property in properties)
            {
                writer.Write(Cell(FormatValue(property.GetValue(row))));
            }

            writer.Write("</row>");
        }

        writer.Write("</sheetData></worksheet>");

        return Task.CompletedTask;
    }

    private static PropertyInfo[] GetExportProperties<TRow>() =>
        typeof(TRow)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetMethod is not null)
            .ToArray();

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string Cell(string? value) =>
        $"<c t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";

    private static string FormatValue(object? value) =>
        value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return escaped.IndexOfAny([',', '\n', '\r', '"']) >= 0
            ? $"\"{escaped}\""
            : escaped;
    }

    private static string EscapeXml(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : SecurityElement.Escape(value) ?? string.Empty;
}
