using System.Linq.Expressions;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Application.UnitTests;

public sealed class ApplicationDbContextTenantFilterTests
{
    [Fact]
    public void TenantFilter_ShouldUseFailClosedExpressionForTenantScopedEntities()
    {
        using var dbContext = CreateContext(tenantId: null);
        var filterText = GetIamRoleTenantFilter(dbContext).ToString();

        Assert.Contains("HasTenantScope", filterText, StringComparison.Ordinal);
        Assert.Contains("CurrentTenantIdOrDefault", filterText, StringComparison.Ordinal);
        Assert.Contains("AndAlso", filterText, StringComparison.Ordinal);
        Assert.DoesNotContain("OrElse", filterText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenTenantScopedEntityHasNoTenantContext_ShouldFailBeforePersisting()
    {
        await using var dbContext = CreateContext(tenantId: null);
        dbContext.IamRoles.Add(IamRole.Create("Security Auditor", "Test role"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());

        Assert.Contains("Tenant-scoped writes require a tenant context.", exception.Message, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=clarihr_filter_tests;Username=postgres;Password=postgres")
            .Options;

        return new ApplicationDbContext(options, new TestTenantContext(tenantId), new FixedDateTimeProvider());
    }

    private static Expression<Func<IamRole, bool>> GetIamRoleTenantFilter(ApplicationDbContext dbContext)
    {
        var filter = dbContext.Model.FindEntityType(typeof(IamRole))?.GetQueryFilter();
        return Assert.IsAssignableFrom<Expression<Func<IamRole, bool>>>(filter);
    }

    private sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = DateTime.Parse("2026-04-18T12:00:00Z").ToUniversalTime();
    }
}
