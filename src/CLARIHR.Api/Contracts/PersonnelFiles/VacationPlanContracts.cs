namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>One planned vacation window of an annual plan (employee publicId + dates + business days).</summary>
public sealed record VacationPlanLineRequest(
    Guid PersonnelFilePublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days);

/// <summary>
/// Body for creating an annual vacation plan (leave module §3.7, D-24). Indicative scheduling: the response
/// returns a non-blocking `warnings[]` per line (availability of the employee's fund, holiday / rest-day).
/// Overlapping windows for the same employee are rejected (`VACATION_PLAN_LINE_OVERLAP`).
/// </summary>
public sealed record AddVacationPlanRequest(
    int PlanYear,
    IReadOnlyCollection<VacationPlanLineRequest> Lines);

/// <summary>Body for replacing the full set of lines of a VIGENTE plan (returns the recomputed warnings).</summary>
public sealed record UpdateVacationPlanRequest(
    IReadOnlyCollection<VacationPlanLineRequest> Lines);

/// <summary>
/// Body for the company-wide vacation-requests bandeja query. Every filter is optional; when <c>StatusCode</c>
/// is omitted the items cover every status (the StatusCounts are always over every status).
/// </summary>
public sealed record QueryVacationRequestsRequest(
    Guid? EmployeeId,
    string? StatusCode,
    DateTime? StartFromUtc,
    DateTime? StartToUtc,
    int? PageNumber,
    int? PageSize);
