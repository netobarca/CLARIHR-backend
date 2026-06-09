using System.Net;
using System.Text;
using System.Text.Json;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Api.IntegrationTests;

public sealed class AccountCompanyAuthorizationIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    [Fact]
    public async Task AccountCompanyAuthorization_RoleCreationFlow_ShouldAcceptLegacyPermissionIdsPayload()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateAuthorizationAdminContext(scenario));

        var createRoleResponse = await client.PostAsync(
            $"/api/v1/account/companies/{scenario.TenantId}/authorization/roles",
            CreateJsonContent($$"""
            {
              "name": "Administrador de Empleados",
              "description": "",
              "permissionIds": [
                "{{scenario.ActorPermissionId}}"
              ]
            }
            """));

        var createRoleBody = await createRoleResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, createRoleResponse.StatusCode);

        using var createdRoleDocument = JsonDocument.Parse(createRoleBody);
        var createdRole = createdRoleDocument.RootElement;
        var createdRolePublicId = createdRole.GetProperty("publicId").GetGuid();
        var createdRoleGrants = createdRole.GetProperty("grants");
        Assert.Equal(1, createdRoleGrants.GetArrayLength());
        Assert.Equal(scenario.ActorPermissionId, createdRoleGrants[0].GetProperty("publicId").GetGuid());

        var updateGrantsResponse = await client.PutAsync(
            $"/api/v1/account/companies/{scenario.TenantId}/authorization/roles/{createdRolePublicId}/grants",
            CreateJsonContent($$"""
            {
              "permissionIds": [
                "{{scenario.ActorPermissionId}}"
              ]
            }
            """));

        var updateGrantsBody = await updateGrantsResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, updateGrantsResponse.StatusCode);

        using var updatedGrantsDocument = JsonDocument.Parse(updateGrantsBody);
        var updatedGrants = updatedGrantsDocument.RootElement;
        Assert.Equal(createdRolePublicId, updatedGrants.GetProperty("rolePublicId").GetGuid());
        var grants = updatedGrants.GetProperty("grants");
        Assert.Equal(1, grants.GetArrayLength());
        Assert.Equal(scenario.ActorPermissionId, grants[0].GetProperty("publicId").GetGuid());
    }

    private static StringContent CreateJsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static TestUserContext CreateAuthorizationAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.SecurityAdminUserId,
            scenario.TenantId,
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Create),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Permissions, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Permissions, RbacPermissionAction.Update));
}
