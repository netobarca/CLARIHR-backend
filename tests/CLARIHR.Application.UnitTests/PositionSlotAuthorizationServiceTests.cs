using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.PositionSlots;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §X-TEST1: unit coverage of <see cref="PositionSlotAuthorizationService"/> — the
/// permission-code matching matrix and the pre-DB deny gates that integration only
/// exercises end-to-end. These scenarios all return BEFORE the permission DB probe
/// (`TenantPermissionGrantEvaluator`), so they need no database; the no-grant→403 path
/// (probe returns false) stays covered by integration
/// (`PositionSlots_List_WithoutPermission_ShouldReturn403`,
/// `PositionSlots_List_WithTenantMismatch_ShouldReturn403`).
/// </summary>
public sealed class PositionSlotAuthorizationServiceTests
{
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    // ---- pre-DB deny gates ----

    [Fact]
    public async Task EvaluateAccessAsync_WhenUnauthenticated_ShouldFailUnauthenticated()
    {
        var service = CreateService(authenticated: false, tenantId: CompanyId, moduleEnabled: true);

        var result = await service.EvaluateAccessAsync(CompanyId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthorizationErrors.Unauthenticated.Code, result.Error.Code);
    }

    [Fact]
    public async Task EnsureCanReadAsync_WhenUnauthenticated_ShouldFailUnauthenticated()
    {
        var service = CreateService(authenticated: false, tenantId: CompanyId, moduleEnabled: true);

        var result = await service.EnsureCanReadAsync(CompanyId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthorizationErrors.Unauthenticated.Code, result.Error.Code);
    }

    [Fact]
    public async Task EnsureCanReadAsync_WhenTenantMismatch_ShouldFailWithReadAction()
    {
        var service = CreateService(
            authenticated: true,
            tenantId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            moduleEnabled: true,
            permissions: PositionSlotPermissionCodes.Read);

        var result = await service.EnsureCanReadAsync(CompanyId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PositionSlotErrors.TenantMismatch(RbacPermissionAction.Read).Code, result.Error.Code);
    }

    [Fact]
    public async Task EnsureCanManageAsync_WhenTenantMismatch_ShouldFailWithUpdateAction()
    {
        var service = CreateService(
            authenticated: true,
            tenantId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            moduleEnabled: true,
            permissions: PositionSlotPermissionCodes.Admin);

        var result = await service.EnsureCanManageAsync(CompanyId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PositionSlotErrors.TenantMismatch(RbacPermissionAction.Update).Code, result.Error.Code);
    }

    [Fact]
    public async Task EvaluateAccessAsync_WhenModuleDisabled_ShouldFailForbidden()
    {
        var service = CreateService(
            authenticated: true,
            tenantId: CompanyId,
            moduleEnabled: false,
            permissions: PositionSlotPermissionCodes.Admin);

        var result = await service.EvaluateAccessAsync(CompanyId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PositionSlotErrors.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task EnsureCanManageAsync_WhenModuleDisabled_ShouldFailForbidden()
    {
        var service = CreateService(
            authenticated: true,
            tenantId: CompanyId,
            moduleEnabled: false,
            permissions: PositionSlotPermissionCodes.Admin);

        var result = await service.EnsureCanManageAsync(CompanyId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PositionSlotErrors.Forbidden.Code, result.Error.Code);
    }

    // ---- claim → access matching matrix (claims grant before any DB probe) ----

    [Theory]
    [InlineData(PositionSlotPermissionCodes.Read)]
    [InlineData(PositionSlotPermissionCodes.Admin)]
    [InlineData(PositionSlotPermissionCodes.ManageAdministration)]
    public async Task EnsureCanReadAsync_WithReadGrantingClaim_ShouldSucceed(string permission)
    {
        var service = CreateService(authenticated: true, tenantId: CompanyId, moduleEnabled: true, permissions: permission);

        var result = await service.EnsureCanReadAsync(CompanyId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(PositionSlotPermissionCodes.Admin)]
    [InlineData(PositionSlotPermissionCodes.ManageAdministration)]
    public async Task EnsureCanManageAsync_WithManageGrantingClaim_ShouldSucceed(string permission)
    {
        var service = CreateService(authenticated: true, tenantId: CompanyId, moduleEnabled: true, permissions: permission);

        var result = await service.EnsureCanManageAsync(CompanyId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(PositionSlotPermissionCodes.Admin)]
    [InlineData(PositionSlotPermissionCodes.ManageAdministration)]
    public async Task EvaluateAccessAsync_WithManageGrantingClaim_ShouldGrantReadAndManage(string permission)
    {
        var service = CreateService(authenticated: true, tenantId: CompanyId, moduleEnabled: true, permissions: permission);

        var result = await service.EvaluateAccessAsync(CompanyId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.CanRead);
        Assert.True(result.Value.CanManage);
    }

    [Fact]
    public async Task EvaluateAccessAsync_IsCaseInsensitiveOnClaimCodes()
    {
        var service = CreateService(
            authenticated: true,
            tenantId: CompanyId,
            moduleEnabled: true,
            permissions: PositionSlotPermissionCodes.Admin.ToLowerInvariant());

        var result = await service.EvaluateAccessAsync(CompanyId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.CanManage);
    }

    private static PositionSlotAuthorizationService CreateService(
        bool authenticated,
        Guid? tenantId,
        bool moduleEnabled,
        params string[] permissions) =>
        new(
            new StubCurrentUserService
            {
                IsAuthenticated = authenticated,
                UserId = authenticated ? Guid.NewGuid().ToString() : null,
                Permissions = permissions
            },
            new StubTenantContext(tenantId),
            new StubPlanEntitlementService(moduleEnabled),
            // Never queried in these scenarios — every case returns before the permission
            // DB probe. A connection is therefore never opened.
            new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql("Host=localhost;Database=clarihr_authz_unit;Username=postgres;Password=postgres")
                    .Options,
                new StubTenantContext(tenantId),
                new FixedDateTimeProvider()));

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated { get; init; }
        public string? UserId { get; init; }
        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();
    }

    private sealed class StubTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class StubPlanEntitlementService(bool moduleEnabled) : IPlanEntitlementService
    {
        public Task EnsureSystemPlanDefaultsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> IsModuleEnabledAsync(Guid companyPublicId, string moduleKey, CancellationToken cancellationToken) =>
            Task.FromResult(moduleEnabled);

        public Task<IReadOnlyCollection<EffectiveCommercialCapabilityGrant>> GetEffectiveCapabilitiesAsync(
            Guid companyPublicId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = DateTime.Parse("2026-05-24T12:00:00Z").ToUniversalTime();
    }
}
