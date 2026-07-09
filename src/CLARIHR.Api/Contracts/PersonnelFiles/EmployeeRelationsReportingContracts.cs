namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Body for the company-wide recognitions bandeja query (REQ-003 RF-012). Every filter is optional; when
/// <c>StatusCode</c> is omitted the items EXCLUDE the ANULADA records unless <c>IncludeAnnulled</c> is true,
/// while the StatusCounts are computed over every status. <c>FromDate</c>/<c>ToDate</c> filter the event date.
/// </summary>
public sealed record QueryRecognitionsRequest(
    Guid? EmployeeId,
    string? RecognitionTypeCode,
    string? StatusCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool? IncludeAnnulled,
    int? PageNumber,
    int? PageSize);

/// <summary>
/// Body for the company-wide disciplinary-actions bandeja query (REQ-003 RF-012). Same status semantics as the
/// recognitions bandeja; <c>FromDate</c>/<c>ToDate</c> filter the incident date (the fault).
/// </summary>
public sealed record QueryDisciplinaryActionsRequest(
    Guid? EmployeeId,
    string? DisciplinaryActionTypeCode,
    string? CauseCode,
    string? StatusCode,
    DateOnly? FromDate,
    DateOnly? ToDate,
    bool? IncludeAnnulled,
    int? PageNumber,
    int? PageSize);
