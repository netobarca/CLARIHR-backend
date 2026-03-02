using System.Security.Claims;
using System.Text.Encodings.Web;
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
            new("sub", userIdValues.ToString())
        };

        if (Request.Headers.TryGetValue(TenantIdHeader, out var tenantIdValues) &&
            !string.IsNullOrWhiteSpace(tenantIdValues.ToString()))
        {
            claims.Add(new Claim("tid", tenantIdValues.ToString()));
        }

        foreach (var role in SplitHeaderValues(RolesHeader))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        foreach (var permission in SplitHeaderValues(PermissionsHeader))
        {
            claims.Add(new Claim("permission", permission));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
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
