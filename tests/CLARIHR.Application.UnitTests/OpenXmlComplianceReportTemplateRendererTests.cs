using CLARIHR.Infrastructure.Reports.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// REQ-016 RF-004 — proves the template-renderer MECHANISM (open template, resolve a defined name to its
/// cell, write the value, save) end-to-end against a minimal placeholder template built in this test.
/// The real F-14/Planilla Única cell-by-cell mapping is NOT covered here — it arrives in PR-6/PR-7 once
/// the business supplies the actual official templates (P-02).
/// </summary>
public sealed class OpenXmlComplianceReportTemplateRendererTests
{
    [Fact]
    public async Task RenderAsync_WritesValueIntoTheCellOfAMatchingDefinedName()
    {
        using var template = BuildPlaceholderTemplate(("EmployerLegalName", "Sheet1!$B$2"));
        using var destination = new MemoryStream();
        var renderer = new OpenXmlComplianceReportTemplateRenderer();

        var missing = await renderer.RenderAsync(
            template,
            new Dictionary<string, string?> { ["EmployerLegalName"] = "Acme El Salvador, S.A. de C.V." },
            destination,
            CancellationToken.None);

        Assert.Empty(missing);
        destination.Position = 0;
        Assert.Equal("Acme El Salvador, S.A. de C.V.", ReadCellValue(destination, "Sheet1", "B2"));
    }

    [Fact]
    public async Task RenderAsync_ReportsNamesWithNoMatchingDefinedName_WithoutThrowing()
    {
        using var template = BuildPlaceholderTemplate(("EmployerLegalName", "Sheet1!$B$2"));
        using var destination = new MemoryStream();
        var renderer = new OpenXmlComplianceReportTemplateRenderer();

        var missing = await renderer.RenderAsync(
            template,
            new Dictionary<string, string?> { ["SomeNameNotInTheTemplate"] = "whatever" },
            destination,
            CancellationToken.None);

        Assert.Single(missing, "SomeNameNotInTheTemplate");
    }

    [Fact]
    public async Task RenderAsync_OverwritesAnExistingCellValue()
    {
        using var template = BuildPlaceholderTemplate(("Total", "Sheet1!$C$3"));
        using var destination1 = new MemoryStream();
        var renderer = new OpenXmlComplianceReportTemplateRenderer();

        await renderer.RenderAsync(
            template, new Dictionary<string, string?> { ["Total"] = "100.00" }, destination1, CancellationToken.None);

        destination1.Position = 0;
        using var destination2 = new MemoryStream();
        await renderer.RenderAsync(
            destination1, new Dictionary<string, string?> { ["Total"] = "250.50" }, destination2, CancellationToken.None);

        destination2.Position = 0;
        Assert.Equal("250.50", ReadCellValue(destination2, "Sheet1", "C3"));
    }

    /// <summary>Builds a minimal, valid, one-sheet .xlsx with the given defined names — the "placeholder template" this PR's mechanism is proven against.</summary>
    private static MemoryStream BuildPlaceholderTemplate(params (string Name, string Reference)[] definedNames)
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });

            var definedNamesElement = new DefinedNames();
            foreach (var (name, reference) in definedNames)
            {
                definedNamesElement.Append(new DefinedName { Name = name, Text = reference });
            }

            workbookPart.Workbook.Append(definedNamesElement);
            workbookPart.Workbook.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static string? ReadCellValue(Stream xlsx, string sheetName, string cellReference)
    {
        using var document = SpreadsheetDocument.Open(xlsx, isEditable: false);
        var workbookPart = document.WorkbookPart!;
        var sheet = workbookPart.Workbook.Sheets!.Elements<Sheet>().Single(s => s.Name == sheetName);
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
        var cell = worksheetPart.Worksheet.GetFirstChild<SheetData>()!
            .Elements<Row>()
            .SelectMany(row => row.Elements<Cell>())
            .SingleOrDefault(c => c.CellReference == cellReference);
        return cell?.CellValue?.Text;
    }
}
