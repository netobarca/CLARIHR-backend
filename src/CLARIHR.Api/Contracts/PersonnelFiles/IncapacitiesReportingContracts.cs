namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for the company-wide incapacities bandeja query. Every filter is optional; when <c>StatusCode</c> is
/// omitted the items default to REGISTRADA (the payroll input excludes EN_REVISION self-registrations), while
/// the StatusCounts are computed over every status.
/// </summary>
public sealed record QueryIncapacitiesRequest(
    Guid? EmployeeId,
    string? RiskCode,
    string? IncapacityTypeCode,
    string? StatusCode,
    string? PayrollTypeCode,
    DateTime? StartFromUtc,
    DateTime? StartToUtc,
    int? PageNumber,
    int? PageSize);
