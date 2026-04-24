using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class PasswordResetLinkBuilder(IOptions<PasswordResetOptions> options) : IPasswordResetLinkBuilder
{
    public string Build(string token)
    {
        var baseUrl = options.Value.FrontendResetUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Password reset frontend URL is not configured.");
        }

        var builder = new UriBuilder(baseUrl);
        var encodedToken = Uri.EscapeDataString(token);
        builder.Query = string.IsNullOrWhiteSpace(builder.Query)
            ? $"token={encodedToken}"
            : $"{builder.Query.TrimStart('?')}&token={encodedToken}";

        return builder.Uri.AbsoluteUri;
    }
}

internal sealed class PasswordResetPolicyProvider(IOptions<PasswordResetOptions> options) : IPasswordResetPolicyProvider
{
    public int GetTokenLifetimeMinutes() => Math.Max(1, options.Value.TokenLifetimeMinutes);

    public int GetCooldownMinutes() => Math.Max(1, options.Value.RequestCooldownMinutes);
}
