using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private const string GoogleMetadataAddress = "https://accounts.google.com/.well-known/openid-configuration";
    private const string GoogleIssuer = "https://accounts.google.com";
    private const string GoogleLegacyIssuer = "accounts.google.com";
    private static readonly IConfigurationManager<OpenIdConnectConfiguration> SharedConfigurationManager = CreateConfigurationManager();

    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly JwtSecurityTokenHandler _tokenHandler = new()
    {
        MapInboundClaims = false
    };

    public GoogleIdTokenValidator()
        : this(SharedConfigurationManager)
    {
    }

    internal GoogleIdTokenValidator(IConfigurationManager<OpenIdConnectConfiguration> configurationManager)
    {
        _configurationManager = configurationManager;
    }

    public async Task<GoogleIdTokenValidationResult?> ValidateAsync(
        string idToken,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        try
        {
            var configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
            var validationParameters = new TokenValidationParameters
            {
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidateIssuer = true,
                ValidIssuers = [GoogleIssuer, GoogleLegacyIssuer],
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            var principal = _tokenHandler.ValidateToken(idToken, validationParameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken)
            {
                return null;
            }

            var subject = ReadClaim(principal, JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(subject))
            {
                return null;
            }

            return new GoogleIdTokenValidationResult(
                subject,
                ReadClaim(principal, JwtRegisteredClaimNames.Email),
                ReadBooleanClaim(principal, "email_verified"),
                ReadClaim(principal, "given_name"),
                ReadClaim(principal, "family_name"),
                ReadClaim(principal, "name"),
                ReadClaim(principal, "hd"));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    private static IConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager() =>
        new ConfigurationManager<OpenIdConnectConfiguration>(
            GoogleMetadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever
            {
                RequireHttps = true
            });

    private static string? ReadClaim(ClaimsPrincipal principal, string claimType) =>
        principal.Claims.FirstOrDefault(claim => claim.Type == claimType)?.Value;

    private static bool ReadBooleanClaim(ClaimsPrincipal principal, string claimType) =>
        bool.TryParse(ReadClaim(principal, claimType), out var value) && value;
}
