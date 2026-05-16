using CLARIHR.Application.Features.EducationCatalogs.Common;

namespace CLARIHR.Application.Features.EducationCatalogs;

/// <summary>
/// Full response for Backoffice administration of an education catalog item.
/// </summary>
public sealed record EducationCatalogItemResponse(
    Guid Id,
    EducationCatalogType CatalogType,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);
