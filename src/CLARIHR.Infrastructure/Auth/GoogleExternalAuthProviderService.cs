using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class GoogleExternalAuthProviderService(
    IGoogleIdTokenValidator googleIdTokenValidator,
    IOptions<GoogleAuthOptions> googleAuthOptions) : IExternalAuthProviderService
{
    public async Task<Result<ExternalAuthValidationResult>> ValidateAsync(
        AuthProvider provider,
        string idToken,
        CancellationToken cancellationToken)
    {
        if (provider != AuthProvider.Google)
        {
            return Result<ExternalAuthValidationResult>.Failure(AuthErrors.ExternalProviderNotSupported);
        }

        var options = googleAuthOptions.Value;
        if (!options.IsConfigured)
        {
            return Result<ExternalAuthValidationResult>.Failure(AuthErrors.ExternalProviderConfigurationInvalid);
        }

        var payload = await googleIdTokenValidator.ValidateAsync(idToken, options.ClientId!, cancellationToken);
        if (payload is null)
        {
            return Result<ExternalAuthValidationResult>.Failure(AuthErrors.ExternalTokenInvalid);
        }

        var email = CleanOptional(payload.Email);
        return Result<ExternalAuthValidationResult>.Success(new ExternalAuthValidationResult(
            Email: email,
            FirstName: ResolveFirstName(payload, email),
            LastName: ResolveLastName(payload),
            ProviderUserId: payload.Subject,
            Provider: AuthProvider.Google,
            CanAutoLinkByEmail: CanAutoLinkByEmail(email, payload.EmailVerified, payload.HostedDomain)));
    }

    private static bool CanAutoLinkByEmail(string? email, bool emailVerified, string? hostedDomain)
    {
        if (!emailVerified || string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        if (email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(hostedDomain);
    }

    private static string ResolveFirstName(GoogleIdTokenValidationResult payload, string? email)
    {
        var firstName = CleanOptional(payload.GivenName) ??
                        ReadFirstNameFromFullName(payload.Name) ??
                        ReadFallbackNameFromEmail(email);

        return string.IsNullOrWhiteSpace(firstName) ? "Google" : firstName;
    }

    private static string ResolveLastName(GoogleIdTokenValidationResult payload)
    {
        var lastName = CleanOptional(payload.FamilyName) ??
                       ReadLastNameFromFullName(payload.Name);

        return string.IsNullOrWhiteSpace(lastName) ? "User" : lastName;
    }

    private static string? ReadFirstNameFromFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        return fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }

    private static string? ReadLastNameFromFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : null;
    }

    private static string? ReadFallbackNameFromEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var localPart = email.Split('@', 2)[0];
        return string.IsNullOrWhiteSpace(localPart) ? null : localPart;
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
