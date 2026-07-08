using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>The plaza (assignment) a settlement values: seniority anchor, occupancy state and cost center (D-10/P-01).</summary>
public sealed record SettlementPlazaContext(
    Guid AssignedPositionPublicId,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    bool IsPrimary,
    string? PositionTitle,
    Guid? CostCenterPublicId,
    string? CostCenterName);

/// <summary>A line suggested from the plaza's compensation config (pending bonus/commission, external installment — D-08).</summary>
public sealed record SettlementSuggestedItemDto(
    string ConceptCode,
    string Description,
    decimal Amount,
    string? CounterpartyName);

/// <summary>ISSS/AFP scheme effective for the employee: instance rates with catalog-default fallback (D-12).</summary>
public sealed record SettlementSchemeDto(
    decimal EmployeeRatePercent,
    decimal EmployerRatePercent,
    decimal? ContributionCap);

/// <summary>One income-tax withholding bracket in force (tabla oficial vigente, MENSUAL).</summary>
public sealed record SettlementTaxBracketDto(
    decimal LowerBound,
    decimal? UpperBound,
    decimal FixedFee,
    decimal RatePercent,
    decimal ExcessOver);

/// <summary>
/// Everything the settlement engine consumes, resolved in ONE trip (pre-development clarification №2:
/// insumos read at create/regenerate and snapshotted — later config changes never silently recalculate).
/// </summary>
public sealed record SettlementCalculationContext(
    SettlementPlazaContext Plaza,
    decimal? MonthlyBaseSalary,
    DateTime? HireDate,
    decimal? ProfileMinimumMonthlyWage,
    DateTime? ProfileRetirementDate,
    IReadOnlyList<SettlementSuggestedItemDto> SuggestedItems,
    SettlementSchemeDto Isss,
    SettlementSchemeDto Afp,
    IReadOnlyList<SettlementTaxBracketDto> RentaBrackets,
    IReadOnlyList<SettlementConceptResponse> Concepts,
    string CurrencyCode,
    decimal? PendingVacationDays = null);

/// <summary>Lookup of a requester candidate (D-06: HR only) — display name + activity + HR-area membership.</summary>
public sealed record SettlementRequesterLookup(
    Guid PersonnelFilePublicId,
    string FullName,
    bool IsActive,
    string? HrFunctionalAreaCode,
    string? OrgUnitFunctionalAreaCode);

/// <summary>
/// The employee's most recent retirement request with the plazas it closed — the anchor of a real
/// settlement (D-03/D-10). <c>StatusCode</c> lets the caller distinguish "not executed" from "reverted".
/// </summary>
public sealed record SettlementRetirementLookup(
    long Id,
    Guid PublicId,
    string StatusCode,
    DateTime RetirementDate,
    string RetirementCategoryCode,
    string? RetirementCategoryNameSnapshot,
    string RetirementReasonCode,
    string? RetirementReasonNameSnapshot,
    IReadOnlyList<Guid> ClosedAssignmentPublicIds);

/// <summary>
/// Dedicated persistence surface of the settlement module (pattern: <c>IExitInterviewRepository</c>):
/// CRUD over <see cref="PersonnelFileSettlement"/> plus the one-stop calculation-context resolver the
/// data-provider step uses. Tenant isolation rides on the EF global query filter.
/// </summary>
public interface ISettlementRepository
{
    /// <summary>
    /// Resolves every engine input for one plaza of one employee: assignment facts (start date — the
    /// per-plaza seniority anchor P-01 — cost center, primary flag), the negotiated base salary
    /// (SALARIO_BASE of the plaza), the suggested items (pending BONO/COMISION incomes and Externo
    /// deductions of the plaza — plus the employee-level ones when the plaza is the principal, P-03),
    /// the effective ISSS/AFP schemes (instance → catalog defaults), the Renta brackets in force, the
    /// settlement-concept catalog of the company's country, the profile facts (hire date, minimum wage,
    /// retirement state) and the company currency. Null when the assignment does not belong to the file.
    /// </summary>
    Task<SettlementCalculationContext?> GetCalculationContextAsync(
        Guid tenantId,
        long personnelFileId,
        Guid assignedPositionPublicId,
        DateTime asOfUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Pending enjoyment days of the employee's active vacation fund (RF-019): Σ over active periods with
    /// <c>GeneratesEnjoymentDays</c> of (granted − net consumed) — the SAME derivation that powers the
    /// profile's <c>VacationDaysAvailable</c>. Null when the module has no fund for the employee, in which
    /// case the engine keeps the legacy <c>DaysSinceAnniversary</c> default for VACACION_PROPORCIONAL
    /// (fully retrocompatible). Resolved into <see cref="SettlementCalculationContext.PendingVacationDays"/>.
    /// </summary>
    Task<decimal?> GetPendingVacationDaysAsync(long personnelFileId, CancellationToken cancellationToken);

    /// <summary>HRIS separation type of a retirement category (drives the suggested compensation line — D-08).</summary>
    Task<RetirementSeparationType?> GetSeparationTypeAsync(Guid tenantId, string retirementCategoryCode, CancellationToken cancellationToken);

    /// <summary>Requester lookup (D-06): display name, activity and functional-area code of its org unit.</summary>
    Task<SettlementRequesterLookup?> GetRequesterLookupAsync(Guid tenantId, Guid personnelFilePublicId, CancellationToken cancellationToken);

    /// <summary>The employee's most recent retirement request (any status) with its closed plazas; null when none exists.</summary>
    Task<SettlementRetirementLookup?> GetLatestRetirementAsync(long personnelFileId, CancellationToken cancellationToken);

    /// <summary>D-16 uniqueness guard: a live real settlement (non-ANULADA, active) already exists for (retirement × plaza).</summary>
    Task<bool> HasLiveSettlementAsync(long retirementRequestId, Guid assignedPositionPublicId, CancellationToken cancellationToken);

    /// <summary>Adds a new settlement (lines included) to the unit of work — no save here.</summary>
    Task AddAsync(PersonnelFileSettlement settlement, CancellationToken cancellationToken);

    /// <summary>Tracked load (lines included) for mutations; null when it does not exist or belongs to another file.</summary>
    Task<PersonnelFileSettlement?> GetTrackedAsync(long personnelFileId, Guid settlementPublicId, CancellationToken cancellationToken);

    /// <summary>Read-only list of the file's settlements and scenarios (active ones), lines included.</summary>
    Task<IReadOnlyCollection<PersonnelFileSettlement>> GetByFileAsync(long personnelFileId, CancellationToken cancellationToken);

    /// <summary>
    /// Tracked live real settlements (non-ANULADA, active) of one retirement request — the reversal hook
    /// (settlement D-17) annuls the drafts and blocks on an issued one.
    /// </summary>
    Task<IReadOnlyCollection<PersonnelFileSettlement>> GetLiveSettlementsForRetirementAsync(
        long retirementRequestId,
        CancellationToken cancellationToken);

    /// <summary>Company-wide bandeja page (RF-006): filters + paging + per-status counts.</summary>
    Task<SettlementBandejaResponse> QuerySettlementsAsync(QuerySettlementsQuery query, CancellationToken cancellationToken);

    /// <summary>Flat export rows of the filtered bandeja (RF-007c), capped at <c>MaxRows</c> when set.</summary>
    Task<IReadOnlyCollection<SettlementExportRow>> GetSettlementExportRowsAsync(ExportSettlementsQuery query, CancellationToken cancellationToken);
}
