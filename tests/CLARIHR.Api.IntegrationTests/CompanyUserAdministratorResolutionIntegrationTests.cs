using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// R1 (re-audit 2026-06-06): <see cref="IUserCompanyRepository.GetActiveAdministratorUserIdsAsync"/>
/// has a fallback branch — taken when no active IAM-linked administrator resolves (incomplete IAM
/// linkage / legacy data) — that resolves administrators from the membership RoleId instead. That
/// branch used to probe <c>IamRoles</c> once per active member with a *synchronous* <c>.Any()</c>
/// inside an in-memory <c>Where</c> (an N+1 of blocking round-trips). It now issues a single
/// set-based query and matches in memory. This guards the fallback against the real provider: it
/// must translate, execute, and resolve EVERY administrator (N &gt; 1) while excluding non-admins —
/// a regression to per-member probing or a broken set query fails here. Mirrors the real-provider
/// repository verification style of <see cref="CompanyUserProvisioningBatchIntegrationTests"/>.
/// </summary>
public sealed class CompanyUserAdministratorResolutionIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    [Fact]
    public async Task GetActiveAdministratorUserIds_WhenResolvedViaMembershipRoleFallback_ResolvesEveryAdministratorAndExcludesNonAdmins()
    {
        var tenantId = Guid.Empty;
        var expectedAdministratorPublicIds = new List<Guid>();
        var nonAdministratorPublicId = Guid.Empty;

        await factory.ResetDatabaseAsync(async dbContext =>
        {
            var countryCatalogItemId = await dbContext.CountryCatalogItems
                .Where(item => item.NormalizedCode == "SV")
                .Select(item => item.Id)
                .SingleAsync();

            // A company whose administrators are resolvable ONLY through the membership-role fallback:
            // we deliberately seed NO IamUser for it, so the IAM-linked administrator path returns
            // empty and GetActiveAdministratorUserIdsAsync drops into the fallback branch under test.
            var company = Company.Create("Fallback Co", "fallback-co", Guid.NewGuid(), "SV", countryCatalogItemId);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();
            tenantId = company.PublicId;

            var administratorRole = IamRole.Create("Fallback Admin", "Administrator resolvable via membership role.");
            administratorRole.SetTenantId(tenantId);
            var employeeRole = IamRole.Create("Fallback Employee", "Non-administrator member.");
            employeeRole.SetTenantId(tenantId);
            dbContext.IamRoles.AddRange(administratorRole, employeeRole);
            await dbContext.SaveChangesAsync();

            var manageAdministration = IamPermission.CreateScreenAction(
                IdentityPermissionCodes.ManageAdministration,
                "Manage Administration",
                "Can manage tenant administration flows.",
                "RBAC",
                "Administration",
                "Manage");
            manageAdministration.SetTenantId(tenantId);
            dbContext.IamPermissions.Add(manageAdministration);
            await dbContext.SaveChangesAsync();

            administratorRole.SyncPermissions([manageAdministration]);
            foreach (var assignment in administratorRole.PermissionAssignments)
            {
                assignment.SetTenantId(tenantId);
            }

            await dbContext.SaveChangesAsync();

            // Two active administrators (N > 1, so a per-member regression would issue N probes) sharing
            // the administrative membership role, plus one active non-administrator that must be excluded.
            for (var index = 0; index < 2; index++)
            {
                var administrator = User.RegisterLocal(
                    "Admin",
                    $"Fallback{index}",
                    $"admin.fallback{index}@fallback-co.test",
                    "hashed-password",
                    country: "SV",
                    source: "integration-tests");
                dbContext.AuthUsers.Add(administrator);
                await dbContext.SaveChangesAsync();

                dbContext.UserCompanyMemberships.Add(UserCompanyMembership.Create(
                    administrator.Id,
                    company.Id,
                    administratorRole.Id,
                    isPrimary: index == 0));
                expectedAdministratorPublicIds.Add(administrator.PublicId);
            }

            var employee = User.RegisterLocal(
                "Regular",
                "Employee",
                "regular.employee@fallback-co.test",
                "hashed-password",
                country: "SV",
                source: "integration-tests");
            dbContext.AuthUsers.Add(employee);
            await dbContext.SaveChangesAsync();

            dbContext.UserCompanyMemberships.Add(UserCompanyMembership.Create(
                employee.Id,
                company.Id,
                employeeRole.Id,
                isPrimary: false));
            nonAdministratorPublicId = employee.PublicId;
        });

        using var scope = factory.Services.CreateScope();
        var userCompanyRepository = scope.ServiceProvider.GetRequiredService<IUserCompanyRepository>();

        var administrators = await userCompanyRepository.GetActiveAdministratorUserIdsAsync(tenantId, CancellationToken.None);

        Assert.Equal(
            expectedAdministratorPublicIds.OrderBy(id => id),
            administrators.OrderBy(id => id));
        Assert.DoesNotContain(nonAdministratorPublicId, administrators);
    }
}
