using System.Text.RegularExpressions;
using CLARIHR.Application.Abstractions.Compliance;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CLARIHR.Infrastructure.Reports.Documents;

/// <summary>
/// REQ-016 RF-004 — see <see cref="IComplianceReportTemplateRenderer"/>. Locates cells by the workbook's
/// OOXML "defined names" (Excel named ranges), never by hardcoded coordinates, so this same mechanism
/// serves any template regardless of exactly where each field sits — the real F-14/Planilla Única
/// templates (P-02) only need to define the right names, no code change here.
/// </summary>
internal sealed partial class OpenXmlComplianceReportTemplateRenderer : IComplianceReportTemplateRenderer
{
    public Task<IReadOnlyCollection<string>> RenderAsync(
        Stream templateSource,
        IReadOnlyDictionary<string, string?> namedCellValues,
        Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(templateSource);
        ArgumentNullException.ThrowIfNull(namedCellValues);
        ArgumentNullException.ThrowIfNull(destination);

        // SpreadsheetDocument needs seekable read/write access; work on an in-memory copy so the
        // caller's template stream (which may be a read-only embedded resource) is never mutated.
        var workingCopy = new MemoryStream();
        templateSource.CopyTo(workingCopy);
        workingCopy.Position = 0;

        var missing = new List<string>();

        using (var document = SpreadsheetDocument.Open(workingCopy, isEditable: true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidOperationException("The compliance report template has no workbook part.");
            var definedNames = workbookPart.Workbook.DefinedNames;

            foreach (var (name, value) in namedCellValues)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var definedName = definedNames?
                    .Elements<DefinedName>()
                    .FirstOrDefault(candidate => string.Equals(candidate.Name?.Value, name, StringComparison.Ordinal));

                if (definedName?.Text is not { } reference ||
                    !TryParseCellReference(reference, out var sheetName, out var cellReference))
                {
                    missing.Add(name);
                    continue;
                }

                var worksheetPart = FindWorksheetPart(workbookPart, sheetName);
                if (worksheetPart is null)
                {
                    missing.Add(name);
                    continue;
                }

                SetCellValue(worksheetPart, cellReference, value);
            }

            workbookPart.Workbook.Save();
        }

        workingCopy.Position = 0;
        workingCopy.CopyTo(destination);
        return Task.FromResult<IReadOnlyCollection<string>>(missing);
    }

    private static WorksheetPart? FindWorksheetPart(WorkbookPart workbookPart, string sheetName)
    {
        var sheet = workbookPart.Workbook.Sheets?
            .Elements<Sheet>()
            .FirstOrDefault(candidate => string.Equals(candidate.Name?.Value, sheetName, StringComparison.Ordinal));

        return sheet?.Id?.Value is { } relationshipId
            ? workbookPart.GetPartById(relationshipId) as WorksheetPart
            : null;
    }

    private static void SetCellValue(WorksheetPart worksheetPart, string cellReference, string? value)
    {
        var worksheet = worksheetPart.Worksheet;
        var sheetData = worksheet.GetFirstChild<SheetData>() ?? worksheet.AppendChild(new SheetData());

        var rowIndex = ParseRowIndex(cellReference);
        var row = sheetData.Elements<Row>().FirstOrDefault(candidate => candidate.RowIndex?.Value == rowIndex);
        if (row is null)
        {
            row = new Row { RowIndex = rowIndex };
            InsertInOrder(sheetData, row, candidate => candidate.RowIndex?.Value ?? 0);
        }

        var cell = row.Elements<Cell>().FirstOrDefault(candidate => candidate.CellReference?.Value == cellReference);
        if (cell is null)
        {
            cell = new Cell { CellReference = cellReference };
            InsertInOrder(row, cell, candidate => ColumnLetterToNumber(candidate.CellReference?.Value ?? cellReference));
        }

        // Always an inline value (mirrors ReportExportFileWriter's "no shared-string-table" simplicity —
        // this renderer only needs to WRITE values, never re-read what it wrote).
        cell.DataType = CellValues.String;
        cell.CellValue = new CellValue(value ?? string.Empty);
    }

    private static void InsertInOrder<TElement, TParent>(TParent parent, TElement element, Func<TElement, uint> sortKey)
        where TElement : DocumentFormat.OpenXml.OpenXmlElement
        where TParent : DocumentFormat.OpenXml.OpenXmlElement
    {
        var key = sortKey(element);
        var after = parent.Elements<TElement>().LastOrDefault(candidate => sortKey(candidate) < key);
        if (after is null)
        {
            parent.InsertAt(element, 0);
        }
        else
        {
            parent.InsertAfter(element, after);
        }
    }

    private static uint ParseRowIndex(string cellReference)
    {
        var digits = CellReferenceRowRegex().Match(cellReference).Value;
        return uint.Parse(digits);
    }

    private static uint ColumnLetterToNumber(string cellReference)
    {
        var letters = CellReferenceColumnRegex().Match(cellReference).Value;
        uint column = 0;
        foreach (var letter in letters)
        {
            column = (column * 26) + (uint)(char.ToUpperInvariant(letter) - 'A' + 1);
        }

        return column;
    }

    /// <summary>Parses an OOXML defined-name reference like <c>Sheet1!$B$2</c> into its sheet and cell parts.</summary>
    private static bool TryParseCellReference(string reference, out string sheetName, out string cellReference)
    {
        sheetName = string.Empty;
        cellReference = string.Empty;

        var separatorIndex = reference.LastIndexOf('!');
        if (separatorIndex <= 0 || separatorIndex == reference.Length - 1)
        {
            return false;
        }

        sheetName = reference[..separatorIndex].Trim('\'');
        cellReference = reference[(separatorIndex + 1)..].Replace("$", string.Empty, StringComparison.Ordinal);
        return cellReference.Length > 0;
    }

    [GeneratedRegex(@"\d+$")]
    private static partial Regex CellReferenceRowRegex();

    [GeneratedRegex(@"^[A-Za-z]+")]
    private static partial Regex CellReferenceColumnRegex();
}
