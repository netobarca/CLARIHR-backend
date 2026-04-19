using System.IO.Compression;
using System.Text;
using CLARIHR.Application.Features.Reports;

namespace CLARIHR.Application.UnitTests;

public sealed class ReportExportFileWriterTests
{
    [Fact]
    public async Task WriteAsync_WhenCsv_ShouldEscapeValuesAndLeaveStreamOpen()
    {
        await using var stream = new MemoryStream();
        var rows = new[]
        {
            new SampleExportRow("ACME", "Needs, quote \"approval\"", 7, true)
        };

        await ReportExportFileWriter.WriteAsync(
            stream,
            rows,
            ReportExportFormats.Csv,
            "Sample",
            CancellationToken.None);

        Assert.True(stream.CanWrite);
        stream.Position = 0;
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("Name,Notes,Count,IsActive", csv, StringComparison.Ordinal);
        Assert.Contains("ACME,\"Needs, quote \"\"approval\"\"\",7,true", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WhenXlsx_ShouldWriteWorkbookEntriesAndEscapeXml()
    {
        await using var stream = new MemoryStream();
        var rows = new[]
        {
            new SampleExportRow("R&D <Core>", "A&B", 3, false)
        };

        await ReportExportFileWriter.WriteAsync(
            stream,
            rows,
            ReportExportFormats.Xlsx,
            "Report",
            CancellationToken.None);

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("xl/workbook.xml"));

        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheetEntry);
        using var reader = new StreamReader(sheetEntry!.Open(), Encoding.UTF8);
        var sheetXml = await reader.ReadToEndAsync(CancellationToken.None);

        Assert.Contains("R&amp;D &lt;Core&gt;", sheetXml, StringComparison.Ordinal);
        Assert.Contains("A&amp;B", sheetXml, StringComparison.Ordinal);
    }

    private sealed record SampleExportRow(
        string Name,
        string Notes,
        int Count,
        bool IsActive);
}
