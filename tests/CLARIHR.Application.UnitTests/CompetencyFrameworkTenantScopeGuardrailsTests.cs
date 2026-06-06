using CLARIHR.Domain.Common;
using CLARIHR.Domain.CompetencyFramework;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Tenant-isolation guardrail for the CompetencyFramework aggregates (audit F10). Cross-tenant scoping
/// in this codebase is enforced by the GLOBAL EF query filter (`ApplicationDbContext.ApplyTenantFilters`),
/// which applies to every <see cref="ITenantScopedEntity"/> — NOT by per-repository explicit WHERE
/// clauses (no repository injects <c>ITenantContext</c>; that would be an anti-pattern here). This test
/// locks in that the CompetencyFramework entities remain <see cref="ITenantScopedEntity"/>, so a future
/// change dropping <c>TenantEntity</c> (which would silently remove them from the global filter's
/// coverage and could leak cross-tenant reads via public-id lookups) fails loudly instead of regressing.
/// </summary>
public sealed class CompetencyFrameworkTenantScopeGuardrailsTests
{
    [Theory]
    [InlineData(typeof(OccupationalPyramidLevel))]
    [InlineData(typeof(CompetencyConduct))]
    [InlineData(typeof(CompetencyConductBehavior))]
    [InlineData(typeof(JobProfileCompetencyExpectation))]
    [InlineData(typeof(JobProfileCompetencyExpectationConduct))]
    public void CompetencyFrameworkEntity_IsTenantScoped(Type entityType)
    {
        Assert.True(
            typeof(ITenantScopedEntity).IsAssignableFrom(entityType),
            $"{entityType.Name} must implement ITenantScopedEntity so the global tenant query filter " +
            "(ApplicationDbContext.ApplyTenantFilters) scopes its public-id reads — the canonical " +
            "cross-tenant isolation layer for the CompetencyFramework repository.");
    }
}
