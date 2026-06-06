using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Application.UnitTests;

public sealed class ConcurrencyTokenMappingGuardrailsTests
{
    [Fact]
    public void EveryEntityWithConcurrencyTokenProperty_ShouldMapItAsIsConcurrencyToken()
    {
        using var dbContext = CreateContext();
        var violations = new List<string>();
        var observed = new List<Type>();

        foreach (var entityType in dbContext.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (clrType.GetProperty("ConcurrencyToken") is null)
            {
                continue;
            }

            observed.Add(clrType);

            var property = entityType.FindProperty("ConcurrencyToken");
            if (property is null)
            {
                violations.Add($"{clrType.FullName}: ConcurrencyToken property is not mapped by EF.");
                continue;
            }

            if (!property.IsConcurrencyToken)
            {
                violations.Add($"{clrType.FullName}: ConcurrencyToken column is mapped but missing .IsConcurrencyToken() (lost-update protection at SaveChanges is disabled).");
            }
        }

        Assert.Empty(violations);
        // Sanity check: the §1-bis remediation targets the PDC aggregates and their JobProfile sibling,
        // plus the CompetencyFramework aggregates (OccupationalPyramidLevel / CompetencyConduct /
        // JobProfileCompetencyExpectation); if these ever disappear from the model the drift-proof loop
        // above passes vacuously.
        Assert.Contains(typeof(PositionDescriptionCatalogItem), observed);
        Assert.Contains(typeof(PositionCategoryClassification), observed);
        Assert.Contains(typeof(PositionCategory), observed);
        Assert.Contains(typeof(JobProfile), observed);
        Assert.Contains(typeof(OccupationalPyramidLevel), observed);
        Assert.Contains(typeof(CompetencyConduct), observed);
        Assert.Contains(typeof(JobProfileCompetencyExpectation), observed);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=clarihr_concurrency_guardrails;Username=postgres;Password=postgres")
            .Options;

        return new ApplicationDbContext(options, new TestTenantContext(null), new FixedDateTimeProvider());
    }

    private sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = DateTime.Parse("2026-05-19T12:00:00Z").ToUniversalTime();
    }
}
