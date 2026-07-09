using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Group-by dimensions (RF-008 / №14) ─────────────────────────────────────────────────────────────────

/// <summary>
/// The 8 whitelisted dimensions of the advanced-search aggregation (№14). An out-of-whitelist token is a shape
/// error (400, <c>ONE_TIME_INCOME_GROUP_DIMENSION_INVALID</c>) rather than a silent no-op. The aggregation always
/// runs a COMPOSITE key (dimension, currency) so the amount totals never cross currencies (RN-13).
/// </summary>
public enum OneTimeIncomeGroupDimension
{
    Estado,
    Tipo,
    Empleado,
    TipoPlanilla,
    Periodo,
    CentroCosto,
    Moneda,
    Mes,
}

/// <summary>Parsing + whitelist of the group-by dimension tokens (case-insensitive). The canonical Spanish tokens
/// are the public contract; the allowed list travels in the 400 error message.</summary>
public static class OneTimeIncomeGroupDimensions
{
    public static readonly IReadOnlyList<string> Allowed =
        new[] { "estado", "tipo", "empleado", "tipoPlanilla", "periodo", "centroCosto", "moneda", "mes" };

    public static bool TryParse(string? raw, out OneTimeIncomeGroupDimension dimension)
    {
        dimension = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        switch (raw.Trim().ToLowerInvariant())
        {
            case "estado": dimension = OneTimeIncomeGroupDimension.Estado; return true;
            case "tipo": dimension = OneTimeIncomeGroupDimension.Tipo; return true;
            case "empleado": dimension = OneTimeIncomeGroupDimension.Empleado; return true;
            case "tipoplanilla": dimension = OneTimeIncomeGroupDimension.TipoPlanilla; return true;
            case "periodo": dimension = OneTimeIncomeGroupDimension.Periodo; return true;
            case "centrocosto": dimension = OneTimeIncomeGroupDimension.CentroCosto; return true;
            case "moneda": dimension = OneTimeIncomeGroupDimension.Moneda; return true;
            case "mes": dimension = OneTimeIncomeGroupDimension.Mes; return true;
            default: return false;
        }
    }
}

// ── Bandeja de ingresos eventuales (RF-008) ────────────────────────────────────────────────────────────

/// <summary>
/// A row of the company-wide one-time-income bandeja (RF-008): one income per employee with its header + value +
/// destination + lifecycle status. Every status is represented (annulled / rejected included with their status —
/// a status filter is available). User ids are null-safe (a non-Guid principal → null — lesson REQ-003).
/// </summary>
public sealed record OneTimeIncomeListItemResponse(
    Guid OneTimeIncomePublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    DateOnly IncomeDate,
    string? Reference,
    string ConceptTypeCode,
    string ConceptNameSnapshot,
    bool IsFixedValue,
    string? CalculationMethod,
    decimal Amount,
    string CurrencyCode,
    Guid AssignedPositionPublicId,
    Guid CostCenterPublicId,
    string CostCenterNameSnapshot,
    Guid RequesterFilePublicId,
    string RequesterNameSnapshot,
    string PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    string PayrollPeriodLabel,
    DateOnly? PayrollPeriodEndDate,
    string StatusCode,
    Guid? RequestedByUserId,
    Guid? DecidedByUserId)
{
    [JsonIgnore]
    public Guid Id => OneTimeIncomePublicId;
}

/// <summary>
/// One aggregation bucket (№14): the dimension key (a stable value — a code, a Guid, or a <c>yyyy-MM</c> month), a
/// display label, the row count and the amount totals BY CURRENCY (RN-13 — never a single cross-currency total).
/// </summary>
public sealed record OneTimeIncomeGroupResponse(
    string Key,
    string KeyLabel,
    int Count,
    IReadOnlyDictionary<string, decimal> TotalsByCurrency);

/// <summary>
/// The bandeja page: items + paging + per-status counts (over the full non-status filter, so every status is
/// represented even when the items are narrowed to a status), the amount totals BY CURRENCY of the filtered set
/// (RN-13) and — when <c>groupBy</c> is present — the aggregation buckets. The groups CUADRAN by construction
/// against the flat totals of the same filter (Σ group.count = totalCount; Σ group.totalsByCurrency = totalsByCurrency).
/// </summary>
public sealed record OneTimeIncomeBandejaResponse(
    IReadOnlyCollection<OneTimeIncomeListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyDictionary<string, decimal> TotalsByCurrency,
    IReadOnlyCollection<OneTimeIncomeGroupResponse>? Groups);

// ── Export rows (Spanish property names → column headers) ───────────────────────────────────────────────

/// <summary>An export row of the one-time-income bandeja. The Excel/CSV/JSON writer turns the public property names
/// into column headers (reflection), so they are the Spanish headers seen by HR.</summary>
public sealed record IngresoEventualExportRow(
    string Empleado,
    string? CodigoEmpleado,
    DateOnly FechaIngreso,
    string? Referencia,
    string Tipo,
    string Concepto,
    bool ValorFijo,
    string? Metodo,
    decimal Monto,
    string Moneda,
    string Plaza,
    string CentroCosto,
    string TipoPlanilla,
    string Periodo,
    string Solicitante,
    string Estado,
    string? RegistradoPor,
    string? DecididoPor);

/// <summary>An export row of the pending / overdue tray (Spanish property names → headers).</summary>
public sealed record IngresoEventualPendienteExportRow(
    string Empleado,
    string? CodigoEmpleado,
    DateOnly FechaIngreso,
    string Tipo,
    string Concepto,
    decimal Monto,
    string Moneda,
    string TipoPlanilla,
    string Periodo,
    DateOnly? FinPeriodo,
    bool Vencido);

/// <summary>
/// An export row of the PAYROLL INPUT for an external payroll system: the pending (AUTORIZADO, not yet applied)
/// one-time incomes of a mandatory payroll type + period. Cuadra EXACTLY against the pending tray of the same
/// filter (excludes annulled and applied). The Spanish property names are the export headers.
/// </summary>
public sealed record InsumoPlanillaEventualExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Concepto,
    string TipoPlanilla,
    string Periodo,
    decimal Monto,
    string Moneda,
    string CentroCosto);

// ── Queries ────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The shared (non-status) filter surface of the bandeja query + export, so the repository composes ONE
/// filter helper over both (the status codes are applied separately — the StatusCounts always span every status).</summary>
public interface IOneTimeIncomeFilters
{
    Guid? EmployeeId { get; }

    string? ConceptTypeCode { get; }

    DateOnly? FromDate { get; }

    DateOnly? ToDate { get; }

    bool? IsFixedValue { get; }

    string? PayrollTypeCode { get; }

    string? PayrollPeriod { get; }

    Guid? CostCenterPublicId { get; }

    string? CurrencyCode { get; }

    Guid? RequesterFilePublicId { get; }

    string? Search { get; }
}

/// <summary>
/// Filters of the one-time-income advanced search + aggregation (RF-008 / №14). Every filter is optional; when
/// <see cref="StatusCodes"/> is empty every status is listed (the StatusCounts always span every status). When
/// <see cref="GroupBy"/> is present the response also carries the aggregation buckets (in the SAME response — the
/// paginated items stand). An invalid <see cref="GroupBy"/> token is a 400 (the handler validates it).
/// </summary>
public sealed record QueryOneTimeIncomesQuery(
    Guid CompanyId,
    IReadOnlyCollection<string>? StatusCodes,
    Guid? EmployeeId,
    string? ConceptTypeCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool? IsFixedValue,
    string? PayrollTypeCode,
    string? PayrollPeriod,
    Guid? CostCenterPublicId,
    string? CurrencyCode,
    Guid? RequesterFilePublicId,
    string? Search,
    string? GroupBy,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<OneTimeIncomeBandejaResponse>, IOneTimeIncomeFilters;

public sealed record ExportOneTimeIncomesQuery(
    Guid CompanyId,
    IReadOnlyCollection<string>? StatusCodes,
    Guid? EmployeeId,
    string? ConceptTypeCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool? IsFixedValue,
    string? PayrollTypeCode,
    string? PayrollPeriod,
    Guid? CostCenterPublicId,
    string? CurrencyCode,
    Guid? RequesterFilePublicId,
    string? Search,
    int? MaxRows) : IQuery<IReadOnlyCollection<IngresoEventualExportRow>>, IOneTimeIncomeFilters;

public sealed record ExportOneTimeIncomePendingQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    bool OnlyOverdue,
    int? MaxRows) : IQuery<IReadOnlyCollection<IngresoEventualPendienteExportRow>>;

/// <summary>The payroll-input export (§5): the pending one-time incomes of a MANDATORY payroll type + period
/// (a missing bound yields 400). Cuadra against the pending tray of the same filter.</summary>
public sealed record ExportOneTimeIncomePayrollInputQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    string? PayrollPeriod,
    int? MaxRows) : IQuery<IReadOnlyCollection<InsumoPlanillaEventualExportRow>>;

// ── Validators ─────────────────────────────────────────────────────────────────────────────────────────

internal sealed class QueryOneTimeIncomesQueryValidator : AbstractValidator<QueryOneTimeIncomesQuery>
{
    public QueryOneTimeIncomesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate!.Value)
            .When(query => query.FromDate.HasValue && query.ToDate.HasValue);
    }
}

internal sealed class ExportOneTimeIncomesQueryValidator : AbstractValidator<ExportOneTimeIncomesQuery>
{
    public ExportOneTimeIncomesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate!.Value)
            .When(query => query.FromDate.HasValue && query.ToDate.HasValue);
    }
}

internal sealed class ExportOneTimeIncomePendingQueryValidator : AbstractValidator<ExportOneTimeIncomePendingQuery>
{
    public ExportOneTimeIncomePendingQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class ExportOneTimeIncomePayrollInputQueryValidator : AbstractValidator<ExportOneTimeIncomePayrollInputQuery>
{
    public ExportOneTimeIncomePayrollInputQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();

        // The mandatory payroll type + period are enforced in the handler as a 400
        // (ONE_TIME_INCOME_PAYROLL_INPUT_FILTER_REQUIRED) so the operator gets an actionable code.
    }
}

// ── Handler-level errors (each needs an EN + ES resource entry — localization parity) ────────────────────

/// <summary>Dedicated errors for the bandeja + exports slice (REQ-006 PR-5): the invalid group dimension (400 with
/// the allowed list) and the mandatory payroll-input filter (400).</summary>
internal static class OneTimeIncomeBandejaErrors
{
    public static Error GroupDimensionInvalid() =>
        new(
            "ONE_TIME_INCOME_GROUP_DIMENSION_INVALID",
            $"The groupBy dimension is invalid. Allowed dimensions: {string.Join(", ", OneTimeIncomeGroupDimensions.Allowed)}.",
            ErrorType.Validation,
            MessageArguments: [string.Join(", ", OneTimeIncomeGroupDimensions.Allowed)]);

    public static readonly Error PayrollInputFilterRequired = new(
        "ONE_TIME_INCOME_PAYROLL_INPUT_FILTER_REQUIRED",
        "A payroll type and a payroll period are required to export the one-time-income payroll input.",
        ErrorType.Validation);
}
