namespace CLARIHR.Api.Contracts.PersonnelFiles;

/// <summary>Body for creating an economic-aid request (employee self-service, or HR on the employee's behalf).</summary>
public sealed record AddEconomicAidRequestRequest(
    string TypeCode,
    string Description,
    decimal RequestedAmount,
    string? CurrencyCode,
    DateTime RequestDateUtc);

/// <summary>Body for editing an economic-aid request's business fields (HR).</summary>
public sealed record UpdateEconomicAidRequestRequest(
    string TypeCode,
    string Description,
    decimal RequestedAmount,
    string? CurrencyCode,
    DateTime RequestDateUtc);

/// <summary>
/// Body for the HR validation action (resolution). <c>TargetStatusCode</c> is one of EN_REVISION,
/// PENDIENTE_DOCUMENTACION, APROBADA or RECHAZADA; <c>ApprovedAmount</c> is required (&gt; 0) when approving.
/// </summary>
public sealed record ResolveEconomicAidRequestRequest(
    string TargetStatusCode,
    decimal? ApprovedAmount,
    string? Notes);

/// <summary>Body for the (informational) disbursement of an approved request.</summary>
public sealed record DisburseEconomicAidRequestRequest(
    decimal DisbursedAmount,
    DateTime DisbursementDateUtc,
    string? PaymentMethodCode);

/// <summary>
/// Body for attaching a supporting document. <c>FilePublicId</c> references an already-uploaded file (purpose
/// <c>EconomicAidRequestDocument</c>); <c>DocumentTypeCatalogItemPublicId</c> is an optional classification.
/// </summary>
public sealed record AddEconomicAidRequestDocumentRequest(
    Guid FilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? Observations);
