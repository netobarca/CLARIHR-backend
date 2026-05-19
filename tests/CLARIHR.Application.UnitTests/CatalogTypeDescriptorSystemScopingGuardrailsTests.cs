using CLARIHR.Domain.CatalogTypes;
using CLARIHR.Domain.Common;
using Xunit;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Drift-proof guardrail for the §D1 finding (doc
/// <c>07-job-profile-catalog-manifest-audit-2026-05-18.md</c>): the catalog-type
/// registry is cached under a GLOBAL (non-tenant) key in
/// <c>CatalogTypeDescriptorRepository.AllCacheKey</c>. That is correct only while
/// <see cref="CatalogTypeDescriptor"/> stays system-scoped — project-foundation.md
/// §12.5 ("caché siempre tenant-scoped") governs tenant-scoped data, and a global
/// cache for system-scoped reference data is the deliberate exemption.
///
/// These tests lock that invariant in executable code so the exemption cannot be
/// silently lost: if anyone makes the entity tenant-scoped (adds
/// <see cref="ITenantScopedEntity"/> / a TenantId), the suite goes red and forces
/// re-scoping the cache key before merge — instead of an auditor re-flagging the
/// global key as a §12.5 violation every pass.
/// </summary>
public class CatalogTypeDescriptorSystemScopingGuardrailsTests
{
    [Fact]
    public void CatalogTypeDescriptor_ShouldBeSystemScoped_NotTenantScoped()
    {
        Assert.True(
            typeof(SystemScopedCatalogItem).IsAssignableFrom(typeof(CatalogTypeDescriptor)),
            "CatalogTypeDescriptor must remain a SystemScopedCatalogItem. The global cache " +
            "key in CatalogTypeDescriptorRepository depends on this; if system-scoping is " +
            "dropped, the cache must be re-scoped per tenant per project-foundation.md §12.5.");

        Assert.False(
            typeof(ITenantScopedEntity).IsAssignableFrom(typeof(CatalogTypeDescriptor)),
            "CatalogTypeDescriptor became tenant-scoped (implements ITenantScopedEntity). " +
            "The GLOBAL cache key in CatalogTypeDescriptorRepository is now a real " +
            "project-foundation.md §12.5 violation: re-scope the cache key per tenant " +
            "(and re-evaluate the catalog-manifest tenant isolation) before merging.");
    }

    [Fact]
    public void CatalogTypeDescriptor_ShouldNotExposeATenantIdMember()
    {
        // Belt-and-suspenders: catches a raw TenantId added without the interface.
        var tenantIdMember = typeof(CatalogTypeDescriptor).GetProperty("TenantId");

        Assert.True(
            tenantIdMember is null,
            "CatalogTypeDescriptor exposes a TenantId. It is no longer platform-global " +
            "reference data — the global cache key in CatalogTypeDescriptorRepository " +
            "must be re-scoped per tenant (project-foundation.md §12.5).");
    }
}
