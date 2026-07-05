using System.ComponentModel.DataAnnotations;
using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Legal parameters of the settlement (RF-011). Every value snapshots into the record; the minimum wage
/// defaults from the employee "ficha" and the rest carry the ratified SV defaults. `aguinaldoDays = 0`
/// means automatic (the engine applies the 15/19/21 seniority tier).
/// </summary>
public sealed record SettlementParametersRequest(
    decimal MinimumMonthlyWage,
    decimal IndemnityCapMultiplier = 4m,
    decimal ResignationCapMultiplier = 2m,
    decimal VacationDays = 15m,
    decimal VacationPremiumPercent = 30m,
    decimal AguinaldoDays = 0m,
    decimal ResignationBenefitDays = 15m,
    int ResignationMinimumServiceYears = 2,
    decimal AguinaldoExemptionMultiplier = 2m,
    int MonthDivisorDays = 30,
    int YearDivisorDays = 365)
{
    public SettlementParametersInputModel ToModel() => new(
        MinimumMonthlyWage,
        IndemnityCapMultiplier,
        ResignationCapMultiplier,
        VacationDays,
        VacationPremiumPercent,
        AguinaldoDays,
        ResignationBenefitDays,
        ResignationMinimumServiceYears,
        AguinaldoExemptionMultiplier,
        MonthDivisorDays,
        YearDivisorDays);
}

/// <summary>
/// Creates a settlement SCENARIO (simulation, D-05): an active plaza of an active employee, an estimated
/// retirement date and a hypothetical category/reason. No side effects of any kind.
/// </summary>
public sealed record AddSettlementScenarioRequest(
    [Required] Guid AssignedPositionPublicId,
    DateTime EstimatedRetirementDate,
    [Required] string RetirementCategoryCode,
    [Required] string RetirementReasonCode,
    DateTime RequestDate,
    // Requester (D-06: HR only). Omit to default to the registering manager's own personnel file.
    Guid? RequesterFilePublicId = null,
    string? Notes = null,
    // Override when the employee "ficha" has no minimum wage registered yet (RN-001.7).
    decimal? MinimumMonthlyWage = null);

/// <summary>
/// Header + parameters edit (recalculates on save). The scenario-only fields (estimated date and
/// hypothetical category/reason) are rejected on a real settlement, whose retirement facts are inherited
/// read-only from the executed retirement (D-03).
/// </summary>
public sealed record UpdateSettlementRequest(
    DateTime RequestDate,
    [Required] SettlementParametersRequest Parameters,
    Guid? RequesterFilePublicId = null,
    string? Notes = null,
    DateTime? EstimatedRetirementDate = null,
    string? RetirementCategoryCode = null,
    string? RetirementReasonCode = null);

/// <summary>
/// One-line adjustment (D-14/RN-002.2): include/exclude, fix or release the days input, set or clear the
/// audited override (reason mandatory), or edit a manual line's description/amount.
/// </summary>
public sealed record UpdateSettlementLineRequest(
    bool? IsIncluded = null,
    decimal? UnitsOrDays = null,
    bool ClearUnitsOverride = false,
    decimal? OverrideAmount = null,
    string? OverrideReason = null,
    bool ClearOverride = false,
    string? Description = null,
    decimal? ManualAmount = null);

/// <summary>Appends a manual line (OTRO_INGRESO / OTRO_DESCUENTO / HORAS_EXTRAS_PENDIENTES…).</summary>
public sealed record AddSettlementManualLineRequest(
    [Required] string ConceptCode,
    [Required] string Description,
    decimal Amount);

/// <summary>
/// Creates a REAL settlement (D-03/D-10): the retirement facts are inherited read-only from the employee's
/// EXECUTED retirement; the plaza must be one of the assignments that retirement closed.
/// </summary>
public sealed record AddSettlementRequest(
    [Required] Guid AssignedPositionPublicId,
    DateTime RequestDate,
    // Requester (D-06: HR only). Omit to default to the registering manager's own personnel file.
    Guid? RequesterFilePublicId = null,
    string? Notes = null,
    // Override when the (locked, retired) employee "ficha" has no minimum wage registered (RN-001.7).
    decimal? MinimumMonthlyWage = null);

/// <summary>Issues the settlement (BORRADOR → EMITIDA). `confirmNegativeNet` acknowledges a negative net pay.</summary>
public sealed record IssueSettlementRequest(bool ConfirmNegativeNet = false);

/// <summary>Annuls the settlement (terminal). The reason is mandatory when annulling an EMITIDA one.</summary>
public sealed record AnnulSettlementRequest(string? Reason = null);

/// <summary>Filters of the company-wide settlements bandeja (RF-006).</summary>
public sealed record QuerySettlementsRequest(
    // "Liquidacion" | "Escenario"; omit for both.
    string? Kind = null,
    string? StatusCode = null,
    string? CategoryCode = null,
    string? ReasonCode = null,
    Guid? EmployeeId = null,
    DateTime? RequestFromUtc = null,
    DateTime? RequestToUtc = null,
    DateTime? RetirementFromUtc = null,
    DateTime? RetirementToUtc = null,
    string? Search = null,
    int? PageNumber = null,
    int? PageSize = null);
