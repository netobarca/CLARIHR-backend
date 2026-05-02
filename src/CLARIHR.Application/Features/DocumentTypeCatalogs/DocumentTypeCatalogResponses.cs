namespace CLARIHR.Application.Features.DocumentTypeCatalogs;

/// <summary>
/// Full response for Backoffice administration of a document type catalog item.
/// </summary>
public sealed record DocumentTypeCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

/// <summary>
/// Lightweight lookup response used for validation and Core API read endpoints.
/// </summary>
public sealed record DocumentTypeCatalogLookup(
    long InternalId,
    Guid Id,
    string Code,
    string Name,
    bool IsActive);
