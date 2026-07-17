namespace CLARIHR.Application.Abstractions.Compliance;

/// <summary>
/// REQ-016 RF-004 (DP-01 of the technical plan): renders a payroll compliance report (F-14, Planilla
/// Única) by writing values into a pre-supplied, blank official `.xlsx` template — located by named
/// ranges ("defined names" in OOXML terms), never by hardcoded cell coordinates, so the same renderer
/// works regardless of exactly which row/column the real template places each field on.
///
/// Deliberately separate from <c>ReportExportFileWriter</c> (REQ-013) — that writer is a flat,
/// reflection-driven, single-style serializer with no cell addressing or merged cells, used by 15+
/// existing export endpoints; extending it in place would risk all of them for the benefit of this one
/// compliance need. This interface mirrors the <c>IDocumentModelRenderer</c> seam already proven for PDF
/// (QuestPDF/Gotenberg) — a narrow, swappable adapter, not a growth of the shared writer.
///
/// This PR ships the MECHANISM only (open template → resolve named cell → write value → save) proven
/// against a placeholder template. The real cell-by-cell mapping for F-14/Planilla Única arrives in
/// PR-6/PR-7, once the business supplies the actual official template files (P-02 of the analysis).
/// </summary>
public interface IComplianceReportTemplateRenderer
{
    /// <summary>
    /// Opens <paramref name="templateSource"/> (a blank `.xlsx`), writes <paramref name="namedCellValues"/>
    /// into the workbook's matching defined names, and writes the result to <paramref name="destination"/>.
    /// A name with no matching defined name in the template is reported back so the caller can decide
    /// whether that is fatal (e.g. a required header field) or safely ignorable.
    /// </summary>
    /// <returns>The subset of <paramref name="namedCellValues"/> keys that had no matching defined name.</returns>
    Task<IReadOnlyCollection<string>> RenderAsync(
        Stream templateSource,
        IReadOnlyDictionary<string, string?> namedCellValues,
        Stream destination,
        CancellationToken cancellationToken);
}
