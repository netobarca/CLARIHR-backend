namespace CLARIHR.Infrastructure.Auth;

internal interface IGoogleIdTokenValidator
{
    Task<GoogleIdTokenValidationResult?> ValidateAsync(
        string idToken,
        string clientId,
        CancellationToken cancellationToken);
}
