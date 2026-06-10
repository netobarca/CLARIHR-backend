using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class EmailVerificationLinkBuilder(IOptions<EmailVerificationOptions> options) : IEmailVerificationLinkBuilder
{
    public string Build(string token)
    {
        var baseUrl = options.Value.FrontendVerifyUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Email verification frontend URL is not configured.");
        }

        var builder = new UriBuilder(baseUrl);
        var encodedToken = Uri.EscapeDataString(token);
        builder.Query = string.IsNullOrWhiteSpace(builder.Query)
            ? $"token={encodedToken}"
            : $"{builder.Query.TrimStart('?')}&token={encodedToken}";

        return builder.Uri.AbsoluteUri;
    }
}

internal sealed class EmailVerificationPolicyProvider(IOptions<EmailVerificationOptions> options) : IEmailVerificationPolicyProvider
{
    public int GetTokenLifetimeMinutes() => Math.Max(1, options.Value.TokenLifetimeMinutes);

    public int GetCooldownMinutes() => Math.Max(1, options.Value.RequestCooldownMinutes);
}
