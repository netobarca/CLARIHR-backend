using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class CompanyUserProvisioningBatchIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    [Fact]
    public async Task ProvisioningBatchRepositoryMethods_ShouldTranslateToSqlAndExecute()
    {
        // PV3: the de-N+1 position-slot role cascade relies on three batch repository queries
        // (IUserRepository.GetByPublicIdsAsync, IUserCompanyRepository.GetMembershipsAsync,
        // IIamAdministrationRepository.GetUsersByTenantAndLinkedUserPublicIdsAsync). Those EF overrides
        // are only exercised against the real provider at runtime — the unit tests use in-memory
        // doubles. This guards their SQL translation: a translation failure throws regardless of
        // seeded data, so arbitrary ids are enough to prove each query translates and executes.
        var scenario = await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var userCompanyRepository = scope.ServiceProvider.GetRequiredService<IUserCompanyRepository>();
        var iamRepository = scope.ServiceProvider.GetRequiredService<IIamAdministrationRepository>();

        var publicIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var users = await userRepository.GetByPublicIdsAsync(publicIds, CancellationToken.None);
        var memberships = await userCompanyRepository.GetMembershipsAsync(new long[] { -1, -2 }, scenario.TenantId, CancellationToken.None);
        var iamUsers = await iamRepository.GetUsersByTenantAndLinkedUserPublicIdsAsync(
            scenario.TenantId,
            publicIds,
            includeRoles: true,
            CancellationToken.None);

        Assert.NotNull(users);
        Assert.NotNull(memberships);
        Assert.NotNull(iamUsers);
    }
}
