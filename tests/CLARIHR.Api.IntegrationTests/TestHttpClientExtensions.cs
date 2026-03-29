using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CLARIHR.Api.IntegrationTests;

internal static class TestHttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();

    public static HttpClient CreateClientFor<TEntryPoint>(this WebApplicationFactory<TEntryPoint> factory, TestUserContext user)
        where TEntryPoint : class
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeader, user.UserId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ClientTypeHeader, user.ClientType.ToClaimValue());

        if (user.TenantId.HasValue)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.TenantIdHeader, user.TenantId.Value.ToString());
        }

        if (user.Roles.Count > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, string.Join(',', user.Roles));
        }

        if (user.Permissions.Count > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.PermissionsHeader, string.Join(',', user.Permissions));
        }

        return client;
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string? requestUri, object? value, CancellationToken cancellationToken = default) =>
        client.PostAsJsonAsync(requestUri, value, JsonOptions, cancellationToken);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string? requestUri, object? value, CancellationToken cancellationToken = default) =>
        client.PutAsJsonAsync(requestUri, value, JsonOptions, cancellationToken);
}

internal sealed record TestUserContext(
    Guid UserId,
    Guid? TenantId,
    AuthClientType ClientType,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions)
{
    public static TestUserContext Authenticated(Guid userId, Guid tenantId, params string[] permissions) =>
        new(userId, tenantId, AuthClientType.Core, [], permissions);

    public static TestUserContext AuthenticatedWithoutTenant(Guid userId, params string[] roles) =>
        new(userId, null, AuthClientType.Core, roles, []);

    public static TestUserContext PlatformAuthenticatedWithoutTenant(Guid userId, params string[] roles) =>
        new(userId, null, AuthClientType.Platform, roles, []);

    public static TestUserContext PlatformAdmin(Guid userId) =>
        PlatformAuthenticatedWithoutTenant(userId);
}
