namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>
/// Filters of the company-wide personnel-actions bandeja (RF-017). Guid `XxxId` fields serialize as
/// `xxxPublicId` on the wire (public-contract convention), matching the dashboard filter names. The date window
/// is resolved server-side: a from/to range if supplied, otherwise the year (+ month), otherwise the current
/// year. `isSystemGenerated` is the origin filter (false = manual, true = automático). SIN MONTOS — the bandeja
/// carries no monetary filter.
/// </summary>
public sealed record QueryCompanyPersonnelActionsRequest(
    string? ActionTypeCode = null,
    string? ActionStatusCode = null,
    bool? IsSystemGenerated = null,
    int? Year = null,
    int? Month = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Guid? EmployeeId = null,
    Guid? FunctionalAreaId = null,
    Guid? OrgUnitId = null,
    Guid? PositionCategoryId = null,
    Guid? JobProfileId = null,
    Guid? WorkCenterId = null,
    string? PayrollTypeCode = null,
    Guid? CostCenterId = null,
    int? PageNumber = null,
    int? PageSize = null);
