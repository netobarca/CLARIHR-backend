namespace CLARIHR.Application.Features.EducationCatalogs;

/// <summary>
/// Internal-only lookup DTO used between repository and handlers to resolve an
/// education catalog item by its public id while carrying the BIGINT
/// <see cref="InternalId"/> needed for FK assignment.
/// <para>
/// MUST NOT be serialized to any request, response, export or public contract:
/// it exposes the internal sequential primary key. Per project foundation §10.3
/// ("Ningún request, response, exportación o contrato público puede exponer
/// <c>id</c> ni <c>internalId</c>") and technical-debt §2.5, anything crossing
/// the API boundary must use the public <see cref="System.Guid"/> identifier
/// only (mirror of <c>CatalogReferenceInternal</c> in PositionDescriptionCatalogs).
/// </para>
/// </summary>
public sealed record EducationCatalogLookupInternal(
    long InternalId,
    Guid Id,
    string Code,
    string Name,
    bool IsActive);
