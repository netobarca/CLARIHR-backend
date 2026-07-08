namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for creating a vacation fund period (D-05). The bounds are derived server-side from
/// <c>useAnniversary</c> (defaulting to the company preference) and the employee's primary-plaza anniversary or
/// the calendar year; the grants default to the company preference when omitted.
/// </summary>
public sealed record AddVacationPeriodRequest(
    int PeriodYear,
    bool? UseAnniversary,
    int? LegalDaysGranted,
    int? BenefitDaysGranted,
    bool? GeneratesEnjoymentDays);

/// <summary>Body for editing the granted days of a period (only allowed while it has no enjoyed days).</summary>
public sealed record UpdateVacationPeriodGrantsRequest(
    int LegalDaysGranted,
    int BenefitDaysGranted);

/// <summary>
/// Body for the company-wide idempotent vacation-fund generation: one active period per active employee for
/// <c>year</c>. The grants and anniversary flag default to the company preference; <c>employeeIds</c> optionally
/// restricts the run.
/// </summary>
public sealed record GenerateVacationPeriodsRequest(
    int Year,
    bool? UseAnniversary,
    int? LegalDaysGranted,
    int? BenefitDaysGranted,
    bool? GeneratesEnjoymentDays,
    IReadOnlyCollection<Guid>? EmployeeIds);

/// <summary>
/// Body for creating a vacation request (D-13). The date range is validated against Art. 178 (a vacation cannot
/// start on a holiday/rest day nor end on a holiday, unless the company preference allows it) and against the
/// employee's fund availability. <c>requestedDays</c> is the number of enjoyed days.
/// </summary>
public sealed record AddVacationRequestRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    int RequestedDays,
    Guid? PlanLinePublicId,
    string? Notes);

/// <summary>One editable fund-period allocation supplied when approving a request (period publicId + days).</summary>
public sealed record VacationAllocationRequestItem(Guid VacationPeriodPublicId, int Days);

/// <summary>
/// Body for deciding a SOLICITADA vacation request. When <c>approve</c> is true the request is approved against
/// <c>allocations</c> (Σ = requested days; an empty/omitted set uses the FIFO suggestion); when false it is
/// rejected (the notes are optional).
/// </summary>
public sealed record DecideVacationRequestRequest(
    bool Approve,
    IReadOnlyCollection<VacationAllocationRequestItem>? Allocations,
    string? Notes);

/// <summary>One editable period → days entry of a return distribution (period publicId + days).</summary>
public sealed record VacationReturnDistributionRequestItem(Guid VacationPeriodPublicId, int Days);

/// <summary>
/// Body for a total/partial return of enjoyed days (D-14). <c>distribution</c> reverses the days to their
/// periods of origin; an empty/omitted set uses the LIFO suggestion.
/// </summary>
public sealed record AddVacationReturnRequest(
    int Days,
    string? Reason,
    IReadOnlyCollection<VacationReturnDistributionRequestItem>? Distribution);
