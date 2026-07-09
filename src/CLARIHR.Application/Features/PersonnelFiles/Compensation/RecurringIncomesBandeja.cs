using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Bandeja de ingresos (RF-010) ──────────────────────────────────────────────────────────────────────

/// <summary>A row of the company-wide recurring-income bandeja (RF-010): one recurring income per employee with
/// its plan header + lifecycle status. Every status is represented (annulled / rejected included with their
/// status — a status filter is available). User ids are null-safe (a non-Guid principal → null).</summary>
public sealed record RecurringIncomeListItemResponse(
    Guid RecurringIncomePublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    string RecurringIncomeTypeCode,
    string ConceptTypeCode,
    string ConceptNameSnapshot,
    Guid AssignedPositionPublicId,
    Guid CostCenterPublicId,
    string CostCenterNameSnapshot,
    DateOnly RegistrationDate,
    string InstallmentFrequencyCode,
    string CurrencyCode,
    string PayrollTypeCode,
    bool IsIndefinite,
    decimal InstallmentValue,
    int? InstallmentCount,
    decimal? TotalAmount,
    string SettlementActionCode,
    string StatusCode,
    Guid? RegisteredByUserId,
    Guid? DecidedByUserId)
{
    [JsonIgnore]
    public Guid Id => RecurringIncomePublicId;
}

/// <summary>The bandeja page: items + paging + per-status counts (over the full non-status filter, so every
/// status is represented even when the items are narrowed to one status).</summary>
public sealed record RecurringIncomeBandejaResponse(
    IReadOnlyCollection<RecurringIncomeListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>
/// An export row of the recurring-income bandeja. The Excel/CSV/JSON writer turns the public property names into
/// column headers (reflection), so the property names are the Spanish headers seen by HR. The plaza is the
/// assignment public id; the cost center travels by its snapshot name; the user ids travel as their raw ids.
/// </summary>
public sealed record IngresoCiclicoExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Tipo,
    string Concepto,
    string Plaza,
    string CentroCosto,
    DateOnly FechaRegistro,
    string Frecuencia,
    decimal ValorCuota,
    int? NumeroCuotas,
    decimal? MontoTotal,
    bool Indefinido,
    string AccionLiquidacion,
    string Estado,
    string Moneda,
    string? RegistradoPor,
    string? DecididoPor);

/// <summary>Filters shared by the recurring-income bandeja and export. Every filter is optional; when
/// <see cref="StatusCode"/> is omitted every status is listed (annulled / rejected included). The StatusCounts
/// are always computed over every status.</summary>
public sealed record QueryRecurringIncomesQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? StatusCode,
    string? RecurringIncomeTypeCode,
    string? PayrollTypeCode,
    DateTime? RegisteredFromUtc,
    DateTime? RegisteredToUtc,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<RecurringIncomeBandejaResponse>;

public sealed record ExportRecurringIncomesQuery(
    Guid CompanyId,
    Guid? EmployeeId,
    string? StatusCode,
    string? RecurringIncomeTypeCode,
    string? PayrollTypeCode,
    DateTime? RegisteredFromUtc,
    DateTime? RegisteredToUtc,
    int? MaxRows) : IQuery<IReadOnlyCollection<IngresoCiclicoExportRow>>;

// ── Bandeja de cuotas pendientes / vencidas (RF-011) ───────────────────────────────────────────────────

/// <summary>
/// One PENDING (unapplied) theoretical installment of a VIGENTE recurring income (RF-011): a pure derivation of
/// the plan (never persisted) whose theoretical due date is on/before the consulted cutoff and is not yet
/// applied. <see cref="IsOverdue"/> marks the ones whose due date is already past (a work item for the next
/// apply-period batch).
/// </summary>
public sealed record RecurringIncomePendingInstallmentResponse(
    Guid RecurringIncomePublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    string RecurringIncomeTypeCode,
    string ConceptNameSnapshot,
    Guid AssignedPositionPublicId,
    string CostCenterNameSnapshot,
    string PayrollTypeCode,
    string CurrencyCode,
    int InstallmentNumber,
    DateOnly TheoreticalDueDate,
    decimal Amount,
    bool IsOverdue);

/// <summary>The pending-installments page: the projected pending / overdue installments + paging.</summary>
public sealed record RecurringIncomePendingInstallmentsResponse(
    IReadOnlyCollection<RecurringIncomePendingInstallmentResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

/// <summary>An export row of the pending-installments bandeja (Spanish property names → headers).</summary>
public sealed record CuotaPendienteCiclicaExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Tipo,
    string Concepto,
    string Plaza,
    string CentroCosto,
    string PayrollType,
    int NumeroCuota,
    DateOnly FechaTeorica,
    decimal Monto,
    string Moneda,
    bool Vencida);

/// <summary>
/// Filters of the pending-installments bandeja / export (RF-011). The cutoff is the payroll-period end date
/// (when <see cref="PayrollPeriodPublicId"/> is supplied), the bare <see cref="CutoffDate"/>, or — if neither —
/// today. <see cref="StartDate"/> (optional) narrows the projection to installments due on/after it. The
/// projection scans only VIGENTE incomes of the (optional) payroll type / employee.
/// </summary>
public sealed record QueryPendingRecurringIncomeInstallmentsQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    DateOnly? CutoffDate,
    DateOnly? StartDate,
    Guid? EmployeeId,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<RecurringIncomePendingInstallmentsResponse>;

public sealed record ExportPendingRecurringIncomeInstallmentsQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    Guid? PayrollPeriodPublicId,
    DateOnly? CutoffDate,
    DateOnly? StartDate,
    Guid? EmployeeId,
    int? MaxRows) : IQuery<IReadOnlyCollection<CuotaPendienteCiclicaExportRow>>;

/// <summary>
/// A VIGENTE recurring income considered by the pending-installments projection, enriched with the employee /
/// plaza metadata needed for the bandeja rows and its applied installment numbers. The handler projects the
/// theoretical pending installments in-memory through the pure <c>RecurringIncomeRules</c>.
/// </summary>
public sealed record RecurringIncomePendingScanItem(
    long InternalId,
    Guid PublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string? EmployeeCode,
    string RecurringIncomeTypeCode,
    string ConceptNameSnapshot,
    Guid AssignedPositionPublicId,
    string CostCenterNameSnapshot,
    string PayrollTypeCode,
    string CurrencyCode,
    bool IsIndefinite,
    string InstallmentFrequencyCode,
    DateOnly InstallmentStartDate,
    decimal InstallmentValue,
    int? InstallmentCount,
    decimal? TotalAmount,
    IReadOnlyCollection<int> AppliedInstallmentNumbers);

// ── Insumo de planilla (RF-012 / §5) ───────────────────────────────────────────────────────────────────

/// <summary>
/// One APPLIED installment of the payroll input for an external payroll system: a row per applied
/// (<c>APLICADA</c>, active) installment of the range with its imputed period. The Spanish property names are the
/// export headers. This export cuadra EXACTLY against the pending installments of the same filter once applied
/// (A.3-10); annulled installments (and their inactive rows) are excluded.
/// </summary>
public sealed record InsumoPlanillaCiclicoExportRow(
    string Empleado,
    string? CodigoEmpleado,
    string Concepto,
    string PayrollType,
    string? PeriodoPlanilla,
    DateOnly FechaAplicada,
    int NumeroCuota,
    decimal Monto,
    string Moneda,
    string CentroCosto);

/// <summary>
/// The payroll-input export (RF-012): the applied installments of a payroll type over a MANDATORY date range
/// (<see cref="StartDate"/> + <see cref="EndDate"/> — a missing bound yields 422). The range is applied over the
/// installment's applied date.
/// </summary>
public sealed record ExportRecurringIncomePayrollInputQuery(
    Guid CompanyId,
    string? PayrollTypeCode,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? MaxRows) : IQuery<IReadOnlyCollection<InsumoPlanillaCiclicoExportRow>>;

// ── Validators ─────────────────────────────────────────────────────────────────────────────────────────

internal sealed class QueryRecurringIncomesQueryValidator : AbstractValidator<QueryRecurringIncomesQuery>
{
    public QueryRecurringIncomesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.RegisteredToUtc)
            .GreaterThanOrEqualTo(query => query.RegisteredFromUtc!.Value)
            .When(query => query.RegisteredFromUtc.HasValue && query.RegisteredToUtc.HasValue);
    }
}

internal sealed class ExportRecurringIncomesQueryValidator : AbstractValidator<ExportRecurringIncomesQuery>
{
    public ExportRecurringIncomesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.RegisteredToUtc)
            .GreaterThanOrEqualTo(query => query.RegisteredFromUtc!.Value)
            .When(query => query.RegisteredFromUtc.HasValue && query.RegisteredToUtc.HasValue);
    }
}

internal sealed class QueryPendingRecurringIncomeInstallmentsQueryValidator
    : AbstractValidator<QueryPendingRecurringIncomeInstallmentsQuery>
{
    public QueryPendingRecurringIncomeInstallmentsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class ExportPendingRecurringIncomeInstallmentsQueryValidator
    : AbstractValidator<ExportPendingRecurringIncomeInstallmentsQuery>
{
    public ExportPendingRecurringIncomeInstallmentsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class ExportRecurringIncomePayrollInputQueryValidator
    : AbstractValidator<ExportRecurringIncomePayrollInputQuery>
{
    public ExportRecurringIncomePayrollInputQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();

        // The mandatory range is enforced in the handler as a 422 (RECURRING_INCOME_PAYROLL_INPUT_RANGE_REQUIRED),
        // not here, so a missing bound is a domain rule (422) rather than a shape error (400).
        RuleFor(query => query.EndDate)
            .GreaterThanOrEqualTo(query => query.StartDate!.Value)
            .When(query => query.StartDate.HasValue && query.EndDate.HasValue);
    }
}
