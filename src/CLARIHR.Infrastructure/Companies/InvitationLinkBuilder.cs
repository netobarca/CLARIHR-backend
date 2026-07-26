using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Companies;

/// <summary>
/// Same shape as <c>PasswordResetLinkBuilder</c>: append <c>token</c> to the configured front-end URL,
/// preserving any query string the deployment already put there.
/// </summary>
internal sealed class InvitationLinkBuilder(IOptions<InvitationOptions> options) : IInvitationLinkBuilder
{
    public string Build(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var baseUrl = options.Value.FrontendAcceptUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                $"Invitation frontend URL is not configured ('{InvitationOptions.SectionName}:FrontendAcceptUrl').");
        }

        var builder = new UriBuilder(baseUrl);
        var encodedToken = Uri.EscapeDataString(token);
        builder.Query = string.IsNullOrWhiteSpace(builder.Query)
            ? $"token={encodedToken}"
            : $"{builder.Query.TrimStart('?')}&token={encodedToken}";

        return builder.Uri.AbsoluteUri;
    }
}
