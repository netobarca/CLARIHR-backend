using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class AccountCompanyAuthorizationIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();

    [Fact]
    public async Task AccountCompanyAuthorization_RoleCreationFlow_ShouldAcceptLegacyPermissionIdsPayload()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateAuthorizationAdminContext(scenario));

        var createRoleResponse = await client.PostAsync(
            RolesUrl(scenario),
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

        // POST → 201 with a Location header and the rotated strong token in both body and ETag header.
        Assert.NotNull(createRoleResponse.Headers.Location);
        Assert.NotNull(createRoleResponse.Headers.ETag);

        using var createdRoleDocument = JsonDocument.Parse(createRoleBody);
        var createdRole = createdRoleDocument.RootElement;
        var createdRolePublicId = createdRole.GetProperty("publicId").GetGuid();
        var roleToken = createdRole.GetProperty("concurrencyToken").GetGuid();
        Assert.NotEqual(Guid.Empty, roleToken);
        var createdRoleGrants = createdRole.GetProperty("grants");
        Assert.Equal(1, createdRoleGrants.GetArrayLength());
        Assert.Equal(scenario.ActorPermissionId, createdRoleGrants[0].GetProperty("publicId").GetGuid());

        // PUT grants now requires the role's strong token in the If-Match header.
        var updateGrantsResponse = await SendWithIfMatchAsync(
            client,
            HttpMethod.Put,
            GrantsUrl(scenario, createdRolePublicId),
            new { permissionIds = new[] { scenario.ActorPermissionId } },
            roleToken);

        var updateGrantsBody = await updateGrantsResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, updateGrantsResponse.StatusCode);

        using var updatedGrantsDocument = JsonDocument.Parse(updateGrantsBody);
        var updatedGrants = updatedGrantsDocument.RootElement;
        Assert.Equal(createdRolePublicId, updatedGrants.GetProperty("rolePublicId").GetGuid());
        var grants = updatedGrants.GetProperty("grants");
        Assert.Equal(1, grants.GetArrayLength());
        Assert.Equal(scenario.ActorPermissionId, grants[0].GetProperty("publicId").GetGuid());

        // The grants write rotates the role's token (grants live in a child table).
        Assert.NotEqual(roleToken, updatedGrants.GetProperty("concurrencyToken").GetGuid());
    }

    [Fact]
    public async Task AccountCompanyAuthorization_UpdateRoleWithoutIfMatch_ShouldReturnBadRequest()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateAuthorizationAdminContext(scenario));

        var rolePublicId = await CreateRoleAsync(client, scenario, "Rol Sin Token");

        // No If-Match header → the strong-token binder rejects the write with 400.
        var response = await client.PutAsync(
            RoleUrl(scenario, rolePublicId),
            CreateJsonContent("""{ "name": "Rol Renombrado", "description": null }"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AccountCompanyAuthorization_UpdateGrantsWithStaleToken_ShouldReturnConflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateAuthorizationAdminContext(scenario));

        var (rolePublicId, staleToken) = await CreateRoleWithTokenAsync(client, scenario, "Rol Concurrente");

        // First grants write consumes the token and rotates it.
        var firstUpdate = await SendWithIfMatchAsync(
            client,
            HttpMethod.Put,
            GrantsUrl(scenario, rolePublicId),
            new { permissionIds = new[] { scenario.ActorPermissionId } },
            staleToken);
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        // Re-using the now-stale token must be rejected with 409 CONCURRENCY_CONFLICT.
        var staleUpdate = await SendWithIfMatchAsync(
            client,
            HttpMethod.Put,
            GrantsUrl(scenario, rolePublicId),
            new { permissionIds = Array.Empty<Guid>() },
            staleToken);

        await AssertProblemDetailsAsync(staleUpdate, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task AccountCompanyAuthorization_PatchRole_ShouldUpdateDescriptionAndRotateToken()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateAuthorizationAdminContext(scenario));

        var (rolePublicId, token) = await CreateRoleWithTokenAsync(client, scenario, "Rol Parcheable");

        var patchResponse = await SendPatchWithIfMatchAsync(
            client,
            RoleUrl(scenario, rolePublicId),
            """[ { "op": "replace", "path": "/description", "value": "Descripción actualizada" } ]""",
            token);

        var patchBody = await patchResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        using var patchedDocument = JsonDocument.Parse(patchBody);
        var patched = patchedDocument.RootElement;
        Assert.Equal("Descripción actualizada", patched.GetProperty("description").GetString());
        // Name was untouched by the patch and must round-trip rather than being wiped.
        Assert.Equal("Rol Parcheable", patched.GetProperty("name").GetString());
        Assert.NotEqual(token, patched.GetProperty("concurrencyToken").GetGuid());
    }

    [Fact]
    public async Task AccountCompanyAuthorization_PatchRoleWithStaleToken_ShouldReturnConflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateAuthorizationAdminContext(scenario));

        var (rolePublicId, staleToken) = await CreateRoleWithTokenAsync(client, scenario, "Rol Parche Conflicto");

        // Consume + rotate the token with a first patch.
        var firstPatch = await SendPatchWithIfMatchAsync(
            client,
            RoleUrl(scenario, rolePublicId),
            """[ { "op": "replace", "path": "/description", "value": "Primera" } ]""",
            staleToken);
        Assert.Equal(HttpStatusCode.OK, firstPatch.StatusCode);

        var stalePatch = await SendPatchWithIfMatchAsync(
            client,
            RoleUrl(scenario, rolePublicId),
            """[ { "op": "replace", "path": "/description", "value": "Segunda" } ]""",
            staleToken);

        await AssertProblemDetailsAsync(stalePatch, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task AccountCompanyAuthorization_SyncUserRoles_ShouldEnforceWeakIfMatch()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateUsersAdminContext(scenario));

        // Target the non-admin actor user (the security admin remains the active administrator, so the
        // last-admin invariant is never tripped). userPublicId is the linked user id.
        var url = $"/api/v1/account/companies/{scenario.TenantId}/authorization/users/{scenario.ActorUserId}/roles";

        // Missing If-Match → 400 (the weak-token binder rejects the write).
        var missing = await client.PutAsync(
            url,
            CreateJsonContent($$"""{ "roleIds": ["{{scenario.TargetRoleId}}"] }"""));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        // Wildcard → 200 (unconditional first write); the rotated weak token comes back in body + ETag.
        var first = await SendWeakIfMatchAsync(client, url, $$"""{ "roleIds": ["{{scenario.TargetRoleId}}"] }""", "*");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotNull(first.Headers.ETag);
        Assert.True(first.Headers.ETag!.IsWeak);

        using var firstDoc = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var weakETag = firstDoc.RootElement.GetProperty("weakETag").GetString();
        Assert.False(string.IsNullOrEmpty(weakETag));

        // Stale/incorrect weak token → 409 CONCURRENCY_CONFLICT.
        var stale = await SendWeakIfMatchAsync(client, url, """{ "roleIds": [] }""", "W/\"0000000000000000000000000000000000000000000000000000000000000000\"");
        await AssertProblemDetailsAsync(stale, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");

        // The correct (current) weak token → 200, and the token rotates again.
        var conditional = await SendWeakIfMatchAsync(client, url, """{ "roleIds": [] }""", $"W/\"{weakETag}\"");
        Assert.Equal(HttpStatusCode.OK, conditional.StatusCode);

        using var conditionalDoc = JsonDocument.Parse(await conditional.Content.ReadAsStringAsync());
        Assert.NotEqual(weakETag, conditionalDoc.RootElement.GetProperty("weakETag").GetString());
    }

    [Fact]
    public async Task AccountCompanyAuthorization_DeleteRole_ShouldWriteAuditEntry()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateAuthorizationAdminContext(scenario));

        var (rolePublicId, token) = await CreateRoleWithTokenAsync(client, scenario, "Rol Auditado Al Borrar");

        var deleteResponse = await SendWithIfMatchAsync(client, HttpMethod.Delete, RoleUrl(scenario, rolePublicId), body: null, token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // A-1: the hard delete must leave a forensic trail (ROLE_DELETED audit entry on the role).
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deletionAudited = await dbContext.AuditLogs
            .IgnoreQueryFilters()
            .AnyAsync(log => log.EntityId == rolePublicId && log.EventType == "ROLE_DELETED");
        Assert.True(deletionAudited);
    }

    [Fact]
    public async Task AccountCompanyAuthorization_DeleteRoleWithoutIfMatch_ShouldReturnBadRequest()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateAuthorizationAdminContext(scenario));

        var rolePublicId = await CreateRoleAsync(client, scenario, "Rol Sin Token Al Borrar");

        // No If-Match header → the strong-token binder rejects the write with 400.
        var deleteResponse = await client.DeleteAsync(RoleUrl(scenario, rolePublicId));

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task AccountCompanyAuthorization_DeleteRoleWithStaleToken_ShouldReturnConflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateAuthorizationAdminContext(scenario));

        var (rolePublicId, staleToken) = await CreateRoleWithTokenAsync(client, scenario, "Rol Conflicto Al Borrar");

        // Rotate the token with an unrelated patch, then try to delete with the now-stale token.
        var firstPatch = await SendPatchWithIfMatchAsync(
            client,
            RoleUrl(scenario, rolePublicId),
            """[ { "op": "replace", "path": "/description", "value": "Actualizado" } ]""",
            staleToken);
        Assert.Equal(HttpStatusCode.OK, firstPatch.StatusCode);

        var staleDelete = await SendWithIfMatchAsync(client, HttpMethod.Delete, RoleUrl(scenario, rolePublicId), body: null, staleToken);

        await AssertProblemDetailsAsync(staleDelete, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT");
    }

    private static async Task<Guid> CreateRoleAsync(HttpClient client, IntegrationTestScenario scenario, string name)
    {
        var (rolePublicId, _) = await CreateRoleWithTokenAsync(client, scenario, name);
        return rolePublicId;
    }

    private static async Task<(Guid RolePublicId, Guid ConcurrencyToken)> CreateRoleWithTokenAsync(
        HttpClient client,
        IntegrationTestScenario scenario,
        string name)
    {
        var response = await client.PostAsync(
            RolesUrl(scenario),
            CreateJsonContent($$"""{ "name": "{{name}}", "description": "", "permissionIds": [] }"""));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        return (root.GetProperty("publicId").GetGuid(), root.GetProperty("concurrencyToken").GetGuid());
    }

    private static string RolesUrl(IntegrationTestScenario scenario) =>
        $"/api/v1/account/companies/{scenario.TenantId}/authorization/roles";

    private static string RoleUrl(IntegrationTestScenario scenario, Guid rolePublicId) =>
        $"{RolesUrl(scenario)}/{rolePublicId}";

    private static string GrantsUrl(IntegrationTestScenario scenario, Guid rolePublicId) =>
        $"{RoleUrl(scenario, rolePublicId)}/grants";

    private static Task<HttpResponseMessage> SendWithIfMatchAsync(
        HttpClient client,
        HttpMethod method,
        string requestUri,
        object? body,
        Guid concurrencyToken)
    {
        var request = new HttpRequestMessage(method, requestUri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        request.Headers.TryAddWithoutValidation("If-Match", concurrencyToken.ToString("D"));
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendPatchWithIfMatchAsync(
        HttpClient client,
        string requestUri,
        string jsonPatch,
        Guid concurrencyToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, requestUri)
        {
            Content = new StringContent(jsonPatch, Encoding.UTF8, "application/json-patch+json")
        };
        request.Headers.TryAddWithoutValidation("If-Match", concurrencyToken.ToString("D"));
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendWeakIfMatchAsync(
        HttpClient client,
        string requestUri,
        string json,
        string ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = CreateJsonContent(json)
        };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return client.SendAsync(request);
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string expectedCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expectedStatusCode, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private static StringContent CreateJsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static TestUserContext CreateAuthorizationAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.SecurityAdminUserId,
            scenario.TenantId,
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Create),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Update),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Delete),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Permissions, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Permissions, RbacPermissionAction.Update));

    private static TestUserContext CreateUsersAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.SecurityAdminUserId,
            scenario.TenantId,
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Update));
}
