using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Features.CommercialPlans.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Infrastructure.Companies;

namespace CLARIHR.Application.UnitTests;

public sealed class CommercialPlanAuthorizationServiceTests
{
    [Fact]
    public async Task EnsurePlatformAdministrationAsync_WhenUserIsUnauthenticated_ShouldReturnUnauthorized()
    {
        var service = new CommercialPlanAuthorizationService(
            new TestCurrentUserService(isAuthenticated: false, userId: null, roles: []));

        var result = await service.EnsurePlatformAdministrationAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthorizationErrors.Unauthenticated.Code, result.Error.Code);
    }

    [Fact]
    public async Task EnsurePlatformAdministrationAsync_WhenRoleIsMissing_ShouldReturnForbidden()
    {
        var service = new CommercialPlanAuthorizationService(
            new TestCurrentUserService(isAuthenticated: true, userId: Guid.NewGuid().ToString(), roles: ["company_admin"]));

        var result = await service.EnsurePlatformAdministrationAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommercialPlanErrors.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task EnsurePlatformAdministrationAsync_WhenPlatformAdminRoleExists_ShouldSucceed()
    {
        var service = new CommercialPlanAuthorizationService(
            new TestCurrentUserService(isAuthenticated: true, userId: Guid.NewGuid().ToString(), roles: ["PLATFORM_ADMIN"]));

        var result = await service.EnsurePlatformAdministrationAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private sealed class TestCurrentUserService(
        bool isAuthenticated,
        string? userId,
        IReadOnlyCollection<string> roles) : ICurrentUserService
    {
        public bool IsAuthenticated { get; } = isAuthenticated;

        public string? UserId { get; } = userId;

        public IReadOnlyCollection<string> Roles { get; } = roles;

        public IReadOnlyCollection<string> Permissions { get; } = [];
    }
}
