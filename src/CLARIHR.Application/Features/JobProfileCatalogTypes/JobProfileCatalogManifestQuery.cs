using CLARIHR.Application.Abstractions.CatalogTypes;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.JobProfileCatalogTypes;

// ─── Response ────────────────────────────────────────────────────────────────

/// <summary>
/// Discovery manifest the frontend reads instead of hardcoding catalog keys.
/// Grouped by Job Profile sub-resource (Q2); sub-resources without catalogs are
/// emitted with an empty <c>fields</c> list so their absence is explicit.
/// </summary>
public sealed record JobProfileCatalogManifestResponse(
    IReadOnlyList<JobProfileCatalogManifestSubResource> SubResources);

public sealed record JobProfileCatalogManifestSubResource(
    string SubResource,
    IReadOnlyList<JobProfileCatalogManifestField> Fields);

public sealed record JobProfileCatalogManifestField(
    string FieldName,
    string Slug,
    string Family,
    string ApiEndpointTemplate,
    string DisplayName,
    bool IsActive);

// ─── Query ───────────────────────────────────────────────────────────────────

public sealed record GetJobProfileCatalogManifestQuery
    : IQuery<JobProfileCatalogManifestResponse>;

// ─── Handler ─────────────────────────────────────────────────────────────────

// Authorization is enforced declaratively by the controller's
// [AuthorizationPolicySet] (GET => JobProfilePolicies.Read). The catalog metadata
// itself is global reference data, so there is no tenant-scoped resource for a
// handler-level gate to check. The only tenant-aware step is convenience: when
// the caller's JWT carries a tenant, the {companyId} placeholder in each
// apiEndpointTemplate is resolved so the frontend gets ready-to-call URLs;
// otherwise the placeholder is left intact (backward compatible).
internal sealed class GetJobProfileCatalogManifestQueryHandler(
    ICatalogTypeDescriptorRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileCatalogManifestQuery, JobProfileCatalogManifestResponse>
{
    public async Task<Result<JobProfileCatalogManifestResponse>> Handle(
        GetJobProfileCatalogManifestQuery query,
        CancellationToken cancellationToken)
    {
        var registry = await repository.GetAllAsync(cancellationToken);
        var registryByCode = registry.ToDictionary(
            entry => entry.Code,
            StringComparer.Ordinal);

        var canonicalByCode = JobProfileCatalogBindingMap.CanonicalTypes.ToDictionary(
            definition => definition.RegistryCode,
            StringComparer.Ordinal);

        // Resolve {companyId} once. No tenant on the JWT => leave the placeholder
        // intact so the contract still works for tenant-less callers. The Internal
        // family template has no placeholder, so Replace is a harmless no-op there.
        var companyId = tenantContext.TenantId;

        var subResources = new List<JobProfileCatalogManifestSubResource>(
            JobProfileCatalogBindingMap.SubResources.Count);

        foreach (var subResource in JobProfileCatalogBindingMap.SubResources)
        {
            var fields = new List<JobProfileCatalogManifestField>();

            foreach (var binding in JobProfileCatalogBindingMap.FieldBindings)
            {
                if (!string.Equals(binding.SubResource, subResource, StringComparison.Ordinal))
                {
                    continue;
                }

                // §D5 (doc technical-debt/07): defense in depth. The loud guarantee
                // that every binding code exists here is the guardrail
                // JobProfileCatalogBindingMapGuardrailsTests
                // .FieldBindings_ShouldReferenceKnownCanonicalCodesAndSubResources
                // (FieldBindings ⊆ CanonicalTypes), so this miss is unreachable at
                // runtime. TryGetValue + skip only ensures that if that guardrail is
                // ever removed AND an orphan binding added, the manifest degrades by
                // omitting that one field instead of throwing KeyNotFoundException → 500.
                if (!canonicalByCode.TryGetValue(binding.RegistryCode, out var definition))
                {
                    continue;
                }

                var normalizedCode = binding.RegistryCode.Trim().ToUpperInvariant();
                registryByCode.TryGetValue(normalizedCode, out var registered);

                var endpointTemplate = JobProfileCatalogBindingMap.ApiEndpointTemplate(
                    definition.Family, definition.Slug);
                var endpoint = companyId is { } resolvedCompanyId
                    ? endpointTemplate.Replace("{companyId}", resolvedCompanyId.ToString())
                    : endpointTemplate;

                fields.Add(new JobProfileCatalogManifestField(
                    binding.FieldName,
                    definition.Slug,
                    definition.Family,
                    endpoint,
                    registered?.Name ?? definition.DisplayName,
                    registered?.IsActive ?? false));
            }

            subResources.Add(new JobProfileCatalogManifestSubResource(subResource, fields));
        }

        return Result<JobProfileCatalogManifestResponse>.Success(
            new JobProfileCatalogManifestResponse(subResources));
    }
}
