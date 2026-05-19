using System.Reflection;
using CLARIHR.Infrastructure.CatalogTypes;
using Xunit;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Drift-proof guardrail for the §D4 freshness contract (doc
/// <c>technical-debt/07-job-profile-catalog-manifest-audit-2026-05-18.md</c>).
///
/// <see cref="CatalogTypeDescriptorRepository"/> caches the registry in a
/// process-local in-memory cache and <c>Invalidate()</c> is not broadcast, so the
/// cross-instance coherency guarantee published on
/// <c>ICatalogTypeDescriptorRepository.GetAllAsync</c> is "eventual, bounded by the
/// TTL (≤ ~3 min)". That documented guarantee is only true while the actual
/// <c>CacheTtl</c> constant stays at or below 3 minutes. This test pins that bound:
/// if anyone raises the TTL, the published freshness contract would silently become
/// a lie — instead this goes red and forces updating the contract deliberately.
/// </summary>
public sealed class CatalogTypeDescriptorCacheFreshnessGuardrailsTests
{
    private static readonly TimeSpan DocumentedMaxStaleness = TimeSpan.FromMinutes(3);

    [Fact]
    public void CacheTtl_ShouldNotExceedTheDocumentedCrossInstanceFreshnessBound()
    {
        var field = typeof(CatalogTypeDescriptorRepository).GetField(
            "CacheTtl",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.True(
            field is not null,
            "CatalogTypeDescriptorRepository.CacheTtl was renamed/removed. The §D4 " +
            "freshness guardrail can no longer pin the documented ≤3 min bound — " +
            "re-point this test and the ICatalogTypeDescriptorRepository contract.");

        var cacheTtl = Assert.IsType<TimeSpan>(field!.GetValue(null));

        Assert.True(
            cacheTtl <= DocumentedMaxStaleness,
            $"CacheTtl is {cacheTtl}, exceeding the documented cross-instance " +
            $"freshness bound of {DocumentedMaxStaleness}. Either lower the TTL or " +
            "update the §D4 freshness contract on ICatalogTypeDescriptorRepository " +
            "(and consider distributed invalidation) — the published guarantee must " +
            "not silently rot.");
    }
}
