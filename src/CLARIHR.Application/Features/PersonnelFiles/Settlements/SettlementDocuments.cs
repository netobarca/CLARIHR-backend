using System.Globalization;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Maps a settlement response to the format-agnostic <see cref="DocumentModel"/> — the boleta de
/// liquidación (RF-007b, D-19 ratificada: PDF en Fase 1, layout estándar). Built on the shared document
/// AST so the PDF engine stays swappable and future formats (DOCX/HTML) reuse this mapping untouched;
/// any future "document per record" module follows this same seam (reusability mandate of D-19).
/// </summary>
public static class SettlementDocumentMapper
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static DocumentModel Map(PersonnelFileSettlementResponse settlement, string employeeFullName)
    {
        var isScenario = settlement.Kind == SettlementKind.Escenario;
        var headerFields = new List<DocumentField>
        {
            new("Empleado", employeeFullName),
            new("Plaza", settlement.PositionName ?? settlement.AssignedPositionPublicId.ToString()),
            new("Tipo", isScenario ? "ESCENARIO (SIMULACIÓN)" : "LIQUIDACIÓN"),
            new("Estado", isScenario ? "SIMULACIÓN — SIN EFECTOS" : settlement.StatusCode ?? string.Empty),
            new("Solicitante", settlement.RequesterName),
            new("Fecha de solicitud", Date(settlement.RequestDate)),
            new(isScenario ? "Fecha de retiro estimada" : "Fecha de retiro", Date(settlement.RetirementDate)),
            new("Categoría", settlement.RetirementCategoryName ?? settlement.RetirementCategoryCode),
            new("Motivo", settlement.RetirementReasonName ?? settlement.RetirementReasonCode),
            new("Inicio de la plaza", Date(settlement.PlazaStartDate)),
            new("Antigüedad (plaza)", $"{settlement.SeniorityYears} años, {settlement.SeniorityDays} días"),
            new("Salario mensual base", Money(settlement.MonthlyBaseSalary, settlement.CurrencyCode)),
        };

        var sections = new List<DocumentSection>
        {
            new("Parámetros aplicados", [new KeyValueBlock(
            [
                new DocumentField("Salario mínimo mensual", Money(settlement.MinimumMonthlyWage, settlement.CurrencyCode)),
                new DocumentField("Tope indemnización", $"{Amount(settlement.IndemnityCapMultiplier)}× mínimo → {Money(settlement.CappedMonthlySalaryIndemnity, settlement.CurrencyCode)}"),
                new DocumentField("Tope renuncia voluntaria", $"{Amount(settlement.ResignationCapMultiplier)}× mínimo → {Money(settlement.CappedMonthlySalaryResignation, settlement.CurrencyCode)}"),
                new DocumentField("Vacación", $"{Amount(settlement.VacationDays)} días + {Amount(settlement.VacationPremiumPercent)}%"),
                new DocumentField("Aguinaldo", settlement.AguinaldoDays > 0 ? $"{Amount(settlement.AguinaldoDays)} días (fijado)" : "Tramo automático 15/19/21 días"),
                new DocumentField("Prestación por renuncia", $"{Amount(settlement.ResignationBenefitDays)} días/año (mínimo {settlement.ResignationMinimumServiceYears} años)"),
                new DocumentField("Exención de aguinaldo", $"{Amount(settlement.AguinaldoExemptionMultiplier)}× mínimo"),
                new DocumentField("Divisores", $"mes {settlement.MonthDivisorDays} / año {settlement.YearDivisorDays}"),
            ])]),
            LinesSection("Ingresos", settlement, SettlementConceptClass.Ingreso),
            LinesSection("Descuentos", settlement, SettlementConceptClass.Descuento),
            LinesSection("Pagos patronales", settlement, SettlementConceptClass.PagoPatronal),
            new("Reserva (provisión contable)", [new KeyValueBlock(
            [
                new DocumentField("Centro de costo", settlement.CostCenterName ?? (settlement.CostCenterPublicId is null ? "Sin centro de costo asignado" : settlement.CostCenterPublicId.ToString()!)),
                new DocumentField("Provisión (ingresos + pagos patronales)", Money(settlement.ProvisionTotal, settlement.CurrencyCode)),
            ])]),
            new("Resumen", [new KeyValueBlock(
            [
                new DocumentField("Total ingresos", Money(settlement.TotalIncomes, settlement.CurrencyCode)),
                new DocumentField("Total descuentos", Money(settlement.TotalDeductions, settlement.CurrencyCode)),
                new DocumentField("NETO A PAGAR", Money(settlement.NetPay, settlement.CurrencyCode)),
                new DocumentField("Total pagos patronales", Money(settlement.TotalEmployerCharges, settlement.CurrencyCode)),
                new DocumentField("Reserva / provisión", Money(settlement.ProvisionTotal, settlement.CurrencyCode)),
            ])]),
        };

        if (settlement.Warnings.Count > 0)
        {
            sections.Add(new DocumentSection(
                "Advertencias",
                [new BulletListBlock(settlement.Warnings
                    .Select(warning => new BulletItem(warning.Code, warning.ConceptCode))
                    .ToArray())]));
        }

        var generatedText = isScenario
            ? "ESCENARIO DE LIQUIDACIÓN — SIMULACIÓN SIN EFECTOS. Cálculo con base en la fecha de retiro estimada; no aplica a la planilla ni modifica ningún dato del empleado (R-10)."
            : "Boleta de liquidación de personal. El cálculo documenta los haberes, descuentos de ley y pagos patronales de la plaza; el pago material se gestiona fuera del sistema (FA-1).";

        return new DocumentModel(
            isScenario ? "Escenario de liquidación (simulación)" : "Boleta de liquidación",
            headerFields,
            generatedText,
            sections);
    }

    private static DocumentSection LinesSection(
        string title,
        PersonnelFileSettlementResponse settlement,
        SettlementConceptClass conceptClass)
    {
        var lines = settlement.Lines
            .Where(line => line.ConceptClass == conceptClass)
            .OrderBy(line => line.SortOrder)
            .ToArray();
        if (lines.Length == 0)
        {
            return new DocumentSection(title, [new MutedTextBlock("Sin líneas en esta sección.")]);
        }

        var rows = lines
            .Select(line => (IReadOnlyList<string>)
            [
                line.ConceptName + (line.IsIncluded ? string.Empty : " (excluida)") + (line.IsZeroByLaw ? " (valor 0 por ley)" : string.Empty),
                line.CalculationDetail ?? line.Description ?? string.Empty,
                line.UnitsOrDays is { } units ? Amount(units) : string.Empty,
                line.CalculationBase is { } baseAmount ? Amount(baseAmount) : string.Empty,
                Amount(line.CalculatedAmount),
                line.OverrideAmount is { } overrideAmount ? Amount(overrideAmount) : string.Empty,
                Amount(line.FinalAmount),
            ])
            .ToArray();

        return new DocumentSection(title,
        [
            new TableBlock(
                [
                    DocumentTableColumn.Relative("Concepto", 2.2f),
                    DocumentTableColumn.Relative("Detalle", 2.8f),
                    DocumentTableColumn.Relative("Días/Factor", 0.9f),
                    DocumentTableColumn.Relative("Base", 0.9f),
                    DocumentTableColumn.Relative("Calculado", 1f),
                    DocumentTableColumn.Relative("Ajustado", 1f),
                    DocumentTableColumn.Relative("Final", 1f),
                ],
                rows),
        ]);
    }

    private static string Date(DateTime value) => value.ToString("yyyy-MM-dd", Culture);

    private static string Amount(decimal value) => value.ToString("0.00##", Culture);

    private static string Money(decimal value, string currencyCode) => $"{value.ToString("0.00", Culture)} {currencyCode}";
}

/// <summary>
/// A flat sectioned row of the INDIVIDUAL tabular export (RF-007a): the reflection-based writer turns the
/// property names into the Spanish headers, and pseudo-rows (ENCABEZADO / PARAMETROS / RESUMEN) carry the
/// non-tabular parts so the whole settlement travels in one sheet.
/// </summary>
public sealed record SettlementDocumentRow(
    string Seccion,
    string Concepto,
    string Detalle,
    string DiasFactor,
    string Base,
    string Calculado,
    string Ajustado,
    string Final,
    string Notas);

/// <summary>Composes the sectioned rows of one settlement for the tabular individual export.</summary>
public static class SettlementDocumentRowComposer
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static IReadOnlyCollection<SettlementDocumentRow> Compose(
        PersonnelFileSettlementResponse settlement,
        string employeeFullName)
    {
        var isScenario = settlement.Kind == SettlementKind.Escenario;
        var rows = new List<SettlementDocumentRow>
        {
            Header("Empleado", employeeFullName),
            Header("Plaza", settlement.PositionName ?? settlement.AssignedPositionPublicId.ToString()),
            Header("Tipo", isScenario ? "ESCENARIO (SIMULACIÓN — SIN EFECTOS)" : "LIQUIDACIÓN"),
            Header("Estado", isScenario ? "SIMULACIÓN" : settlement.StatusCode ?? string.Empty),
            Header("Solicitante", settlement.RequesterName),
            Header("Fecha de solicitud", settlement.RequestDate.ToString("yyyy-MM-dd", Culture)),
            Header(isScenario ? "Fecha de retiro estimada" : "Fecha de retiro", settlement.RetirementDate.ToString("yyyy-MM-dd", Culture)),
            Header("Categoría", settlement.RetirementCategoryName ?? settlement.RetirementCategoryCode),
            Header("Motivo", settlement.RetirementReasonName ?? settlement.RetirementReasonCode),
            Header("Antigüedad (plaza)", $"{settlement.SeniorityYears} años, {settlement.SeniorityDays} días"),
            Header("Salario mensual base", Money(settlement.MonthlyBaseSalary, settlement.CurrencyCode)),
            Header("Salario mínimo aplicado", Money(settlement.MinimumMonthlyWage, settlement.CurrencyCode)),
        };

        AppendLines(rows, settlement, SettlementConceptClass.Ingreso, "INGRESOS");
        AppendLines(rows, settlement, SettlementConceptClass.Descuento, "DESCUENTOS");
        AppendLines(rows, settlement, SettlementConceptClass.PagoPatronal, "PAGOS PATRONALES");

        rows.Add(Summary("Total ingresos", settlement.TotalIncomes, settlement.CurrencyCode));
        rows.Add(Summary("Total descuentos", settlement.TotalDeductions, settlement.CurrencyCode));
        rows.Add(Summary("NETO A PAGAR", settlement.NetPay, settlement.CurrencyCode));
        rows.Add(Summary("Total pagos patronales", settlement.TotalEmployerCharges, settlement.CurrencyCode));
        rows.Add(new SettlementDocumentRow(
            "RESUMEN", "Reserva / provisión (ingresos + patronales)",
            settlement.CostCenterName is null ? "Sin centro de costo asignado" : $"Centro de costo: {settlement.CostCenterName}",
            string.Empty, string.Empty, string.Empty, string.Empty,
            Amount(settlement.ProvisionTotal),
            settlement.CurrencyCode));

        return rows;
    }

    private static void AppendLines(
        List<SettlementDocumentRow> rows,
        PersonnelFileSettlementResponse settlement,
        SettlementConceptClass conceptClass,
        string sectionName)
    {
        foreach (var line in settlement.Lines.Where(line => line.ConceptClass == conceptClass).OrderBy(line => line.SortOrder))
        {
            rows.Add(new SettlementDocumentRow(
                sectionName,
                line.ConceptName + (line.IsIncluded ? string.Empty : " (excluida)") + (line.IsZeroByLaw ? " (valor 0 por ley)" : string.Empty),
                line.CalculationDetail ?? line.Description ?? string.Empty,
                line.UnitsOrDays is { } units ? Amount(units) : string.Empty,
                line.CalculationBase is { } baseAmount ? Amount(baseAmount) : string.Empty,
                Amount(line.CalculatedAmount),
                line.OverrideAmount is { } overrideAmount ? Amount(overrideAmount) : string.Empty,
                Amount(line.FinalAmount),
                line.OverrideReason ?? line.ZeroReasonCode ?? string.Empty));
        }
    }

    private static SettlementDocumentRow Header(string label, string value) => new(
        "ENCABEZADO", label, value, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    private static SettlementDocumentRow Summary(string label, decimal value, string currency) => new(
        "RESUMEN", label, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Amount(value), currency);

    private static string Amount(decimal value) => value.ToString("0.00##", Culture);

    private static string Money(decimal value, string currencyCode) => $"{value.ToString("0.00", Culture)} {currencyCode}";
}
