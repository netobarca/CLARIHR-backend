using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Platform.Common;
using CLARIHR.Domain.Platform;
using CLARIHR.Infrastructure.Platform;

namespace CLARIHR.Application.UnitTests;

public sealed class PlatformAuthorizationServiceTests
{
    [Fact]
    public async Task EnsureCanReadAsync_WhenUserIsUnauthenticated_ShouldReturnUnauthenticated()
    {
        var service = new PlatformAuthorizationService(
            new TestCurrentUserService(isAuthenticated: false, userId: null),
            new TestPlatformOperatorRepository(null));

        var result = await service.EnsureCanReadAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthorizationErrors.Unauthenticated.Code, result.Error.Code);
    }

    [Fact]
    public async Task EnsureCanManageAsync_WhenOperatorIsReadOnly_ShouldReturnForbidden()
    {
        var service = new PlatformAuthorizationService(
            new TestCurrentUserService(isAuthenticated: true, userId: Guid.NewGuid().ToString()),
            new TestPlatformOperatorRepository(CreatePlatformOperator(PlatformOperatorRole.ReadOnly)));

        var result = await service.EnsureCanManageAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PlatformAccessErrors.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task EnsureCanManageAsync_WhenOperatorIsAdmin_ShouldSucceed()
    {
        var service = new PlatformAuthorizationService(
            new TestCurrentUserService(isAuthenticated: true, userId: Guid.NewGuid().ToString()),
            new TestPlatformOperatorRepository(CreatePlatformOperator(PlatformOperatorRole.Admin)));

        var result = await service.EnsureCanManageAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private static PlatformOperator CreatePlatformOperator(PlatformOperatorRole role)
    {
        var platformOperator = PlatformOperator.Create(userId: 10, role);
        if (!platformOperator.IsActive)
        {
            platformOperator.Reactivate();
        }

        return platformOperator;
    }

    private sealed class TestCurrentUserService(bool isAuthenticated, string? userId) : ICurrentUserService
    {
        public bool IsAuthenticated => isAuthenticated;
        public string? UserId => userId;
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => [];
    }

    private sealed class TestPlatformOperatorRepository(PlatformOperator? platformOperator) : IPlatformOperatorRepository
    {
        public void Add(PlatformOperator platformOperator) => throw new NotSupportedException();

        public Task<PlatformOperator?> GetByUserIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult<PlatformOperator?>(null);

        public Task<PlatformOperator?> GetActiveByUserPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(platformOperator is { IsActive: true } ? platformOperator : null);

        public Task<bool> ExistsAnyAsync(CancellationToken cancellationToken) =>
            Task.FromResult(platformOperator is not null);
    }
}
