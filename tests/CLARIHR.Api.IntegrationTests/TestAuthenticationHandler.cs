using System.Security.Claims;
using System.Text.Encodings.Web;
using CLARIHR.Domain.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Api.IntegrationTests;

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IntegrationTest";
    public const string UserIdHeader = "X-Test-UserId";
    public const string TenantIdHeader = "X-Test-TenantId";
    public const string RolesHeader = "X-Test-Roles";
    public const string PermissionsHeader = "X-Test-Permissions";
    public const string ClientTypeHeader = "X-Test-ClientType";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValues) ||
            string.IsNullOrWhiteSpace(userIdValues.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userIdValues.ToString()),
            new("sub", userIdValues.ToString()),
            new("client_type", ResolveClientType())
        };

        if (Request.Headers.TryGetValue(TenantIdHeader, out var tenantIdValues) &&
            !string.IsNullOrWhiteSpace(tenantIdValues.ToString()))
        {
            claims.Add(new Claim("tid", tenantIdValues.ToString()));
        }

        // Roles and permissions are deliberately NOT injected here. They reach the principal the same way
        // they do in production — through EffectiveAccessClaimsTransformation — with
        // HeaderEffectiveAccessResolver reading the X-Test-* headers as the source. Injecting them here as
        // well would give the actor the union of the headers and the seeded IAM rows, which trips the rules
        // that deny for holding too much.
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string ResolveClientType()
    {
        if (!Request.Headers.TryGetValue(ClientTypeHeader, out var values) ||
            string.IsNullOrWhiteSpace(values.ToString()))
        {
            return AuthClientType.Core.ToClaimValue();
        }

        return values.ToString();
    }

    private IEnumerable<string> SplitHeaderValues(string headerName)
    {
        if (!Request.Headers.TryGetValue(headerName, out var values))
        {
            return [];
        }

        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(static value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
