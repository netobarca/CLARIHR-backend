namespace CLARIHR.Application.Features.JobProfileCatalogTypes;

/// <summary>
/// Full response for Backoffice administration of a Job Profile catalog type.
/// API/wire projection of the Domain entity
/// <see cref="CLARIHR.Domain.CatalogTypes.CatalogTypeDescriptor"/> — same entity,
/// layer-specific name by design (§D7, doc technical-debt/07; the full naming map
/// lives on the entity). These property/type names are part of the OpenAPI
/// contract: do not rename to unify with the <c>CatalogTypeDescriptor*</c> side.
/// </summary>
public sealed record JobProfileCatalogTypeResponse(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

/// <summary>
/// Lightweight read projection of
/// <see cref="CLARIHR.Domain.CatalogTypes.CatalogTypeDescriptor"/>, used to join the
/// static catalog binding map against the DB registry when building the Job Profile
/// catalog manifest. Carries the <c>CatalogTypeDescriptor*</c> name (persistence
/// side) though it lives in the feature namespace — see the naming map on the
/// entity (§D7, doc technical-debt/07).
/// </summary>
public sealed record CatalogTypeDescriptorLookup(
    string Code,
    string Name,
    bool IsActive);
