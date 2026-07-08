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
