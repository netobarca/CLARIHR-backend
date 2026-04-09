using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Application.Features.AccountCompanies.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Platform;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.UnitTests;

public sealed class AccountCompanySubscriptionHelperTests
{
    [Fact]
    public async Task EnsureMasterPlanAccessAsync_WhenTargetPlanIsNotMaster_ShouldSucceed()
    {
        var result = await AccountCompanySubscriptionHelper.EnsureMasterPlanAccessAsync(
            Guid.NewGuid(),
            CreatePlan("FREE"),
            new TestPlatformOperatorRepository(platformOperator: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureMasterPlanAccessAsync_WhenTargetPlanIsMasterWithoutActiveOperator_ShouldReturnForbidden()
    {
        var result = await AccountCompanySubscriptionHelper.EnsureMasterPlanAccessAsync(
            Guid.NewGuid(),
            CreatePlan(ProvisioningConstants.MasterPlanCode),
            new TestPlatformOperatorRepository(platformOperator: null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AccountCompanyErrors.MasterPlanForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task EnsureMasterPlanAccessAsync_WhenTargetPlanIsMasterWithActiveOperator_ShouldSucceed()
    {
        var platformOperator = PlatformOperator.Create(userId: 10, PlatformOperatorRole.Admin);
        if (!platformOperator.IsActive)
        {
            platformOperator.Reactivate();
        }

        var result = await AccountCompanySubscriptionHelper.EnsureMasterPlanAccessAsync(
            Guid.NewGuid(),
            CreatePlan(ProvisioningConstants.MasterPlanCode),
            new TestPlatformOperatorRepository(platformOperator),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private static CommercialPlan CreatePlan(string code) =>
        CommercialPlan.Create(
            code,
            code,
            null,
            0m,
            0m,
            CommercialPlanStatus.Active,
            isSystemPlan: string.Equals(code, ProvisioningConstants.MasterPlanCode, StringComparison.Ordinal),
            []);

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
