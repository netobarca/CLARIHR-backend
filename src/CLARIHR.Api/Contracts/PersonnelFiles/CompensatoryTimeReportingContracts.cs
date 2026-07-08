namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for the company-wide compensatory-time movements bandeja query. Every filter is optional. When
/// <c>StatusCode</c> is supplied the items are filtered to exactly that status (so ANULADA can be queried
/// explicitly); otherwise <c>IncludeAnnulled</c> (default false) decides whether the ANULADA movements are
/// shown — the DEFAULT excludes them. <c>OperationCode</c> restricts to <c>ACREDITACION</c> (credits) or
/// <c>AUSENCIA</c> (absences); <c>FromDate</c>/<c>ToDate</c> filter the worked date (credit) / start date
/// (absence). The StatusCounts are computed over EVERY status.
/// </summary>
public sealed record QueryCompensatoryTimeMovementsRequest(
    Guid? EmployeeId,
    Guid? CompensatoryTimeTypePublicId,
    string? OperationCode,
    string? StatusCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool? IncludeAnnulled,
    int? PageNumber,
    int? PageSize);
