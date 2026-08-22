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

    /// <summary>
    /// B-02 — un campo de día se exporta en ISO (`2026-08-15`), igual que viaja por la API.
    /// <para>
    /// <c>DateOnly</c> no tenía caso propio en <c>FormatValue</c> y caía en la rama genérica
    /// <c>IFormattable</c>, que en cultura invariante produce <b><c>08/15/2026</c></b>: ambiguo para quien abre
    /// el CSV y distinto de lo que devuelve el endpoint. Es un defecto <b>preexistente</b> —ya afectaba a los
    /// exports de planilla y vacaciones, que usan <c>DateOnly</c> desde antes—; B-02 lo habría extendido a los
    /// representantes legales.
    /// </para>
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenRowHasADayField_ShouldExportItAsIsoDate()
    {
        await using var stream = new MemoryStream();
        var rows = new[] { new DayExportRow(new DateOnly(2026, 8, 15), new DateOnly(2026, 12, 1)) };

        await ReportExportFileWriter.WriteAsync(
            stream,
            rows,
            ReportExportFormats.Csv,
            "Days",
            CancellationToken.None);

        stream.Position = 0;
        var csv = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("2026-08-15,2026-12-01", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("08/15/2026", csv, StringComparison.Ordinal);
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

    private sealed record DayExportRow(DateOnly EffectiveFrom, DateOnly? EffectiveTo);
}
