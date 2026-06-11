using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Domain.Auth;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class JwtTokenService(
    IOptions<JwtTokenOptions> options,
    IDateTimeProvider dateTimeProvider,
    IUserRepository userRepository,
    IUserCompanyRepository userCompanyRepository,
    IUserPreferenceRepository userPreferenceRepository,
    IIamAdministrationRepository iamRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IRefreshTokenHasher refreshTokenHasher,
    IPlatformAuditService platformAuditService,
    ILogger<JwtTokenService> logger) : ITokenService
{
    private const string LanguageClaimType = "language";
    private const string DefaultLanguage = "en";

    public async Task<Result<AuthTokenResult>> GenerateAsync(User user, CancellationToken cancellationToken)
    {
        var tenantId = await userCompanyRepository.GetActivePrimaryCompanyPublicIdAsync(user.Id, cancellationToken);
        return await GenerateInternalAsync(user, tenantId, AuthClientType.Core, includeAuthorizationClaims: true, cancellationToken);
    }

    public async Task<Result<AuthTokenResult>> GenerateLoginAsync(User user, CancellationToken cancellationToken)
    {
        var tenantId = await userCompanyRepository.GetActivePrimaryCompanyPublicIdAsync(user.Id, cancellationToken);
        return await GenerateInternalAsync(user, tenantId, AuthClientType.Core, includeAuthorizationClaims: false, cancellationToken);
    }

    public Task<Result<AuthTokenResult>> GenerateForTenantAsync(User user, Guid tenantId, CancellationToken cancellationToken) =>
        GenerateInternalAsync(user, tenantId, AuthClientType.Core, includeAuthorizationClaims: true, cancellationToken);

    public Task<Result<AuthTokenResult>> GeneratePlatformAsync(User user, CancellationToken cancellationToken) =>
        GenerateInternalAsync(user, tenantId: null, AuthClientType.Platform, includeAuthorizationClaims: false, cancellationToken);

    private async Task<Result<AuthTokenResult>> GenerateInternalAsync(
        User user,
        Guid? tenantId,
        AuthClientType clientType,
        bool includeAuthorizationClaims,
        CancellationToken cancellationToken)
    {
        var jwtOptions = options.Value;
        if (!jwtOptions.IsConfigured)
        {
            return Result<AuthTokenResult>.Failure(AuthErrors.TokenConfigurationInvalid);
        }

        var issuedAt = dateTimeProvider.UtcNow;
        var expiresAt = issuedAt.AddMinutes(jwtOptions.AccessTokenExpirationMinutes);
        var refreshExpiresAt = issuedAt.AddDays(jwtOptions.RefreshTokenExpirationDays);

        var claims = await CreateIdentityClaimsAsync(user, tenantId, clientType, includeAuthorizationClaims, cancellationToken);

        var signingCredentials = new SigningCredentials(
            JwtConfigurationDiagnostics.CreateSigningKey(jwtOptions.SigningKey!),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: ResolveAudience(jwtOptions, clientType),
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        var serializedToken = new JwtSecurityTokenHandler().WriteToken(token);
        var rawRefreshToken = CreateRefreshTokenValue();
        var refreshToken = RefreshToken.Issue(
            user.Id,
            clientType,
            refreshTokenHasher.Hash(rawRefreshToken),
            refreshExpiresAt);

        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        var expiresIn = (int)Math.Round((expiresAt - issuedAt).TotalSeconds);
        logger.LogInformation(
            "Issued auth token pair for user {UserPublicId} provider {AuthProvider} client type {ClientType} audience {Audience} key fingerprint {KeyFingerprint}",
            user.PublicId,
            user.AuthProvider,
            clientType,
            ResolveAudience(jwtOptions, clientType),
            JwtConfigurationDiagnostics.ComputeSigningKeyFingerprint(jwtOptions.SigningKey));

        return Result<AuthTokenResult>.Success(new AuthTokenResult(
            serializedToken,
            RefreshToken: rawRefreshToken,
            ExpiresIn: expiresIn));
    }

    public async Task<Result<RefreshTokenExchangeResult>> RefreshAsync(
        string refreshToken,
        AuthClientType clientType,
        CancellationToken cancellationToken)
    {
        var jwtOptions = options.Value;
        if (!jwtOptions.IsConfigured)
        {
            return Result<RefreshTokenExchangeResult>.Failure(AuthErrors.TokenConfigurationInvalid);
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<RefreshTokenExchangeResult>.Failure(AuthErrors.RefreshTokenInvalid);
        }

        var utcNow = dateTimeProvider.UtcNow;
        var refreshTokenHash = refreshTokenHasher.Hash(refreshToken);
        var storedToken = await refreshTokenRepository.GetByTokenHashAsync(refreshTokenHash, cancellationToken);
        if (storedToken is null)
        {
            logger.LogWarning("Refresh token rejected because it was not found");
            return Result<RefreshTokenExchangeResult>.Failure(AuthErrors.RefreshTokenInvalid);
        }

        if (storedToken.ClientType != clientType)
        {
            logger.LogWarning(
                "Refresh token rejected because client type {StoredClientType} did not match expected {ExpectedClientType}",
                storedToken.ClientType,
                clientType);
            return Result<RefreshTokenExchangeResult>.Failure(AuthErrors.RefreshTokenInvalid);
        }

        if (!storedToken.IsActive(utcNow))
        {
            if (storedToken.HasBeenRotated)
            {
                await refreshTokenRepository.RevokeFamilyAsync(
                    storedToken.FamilyId,
                    utcNow,
                    "reuse-detected",
                    cancellationToken);

                // AU-7: record the token-theft signal in the durable audit trail (not just an app log), with
                // the affected user and the auto-captured source IP / user-agent.
                var compromisedUser = await userRepository.GetByIdAsync(storedToken.UserId, cancellationToken);
                await platformAuditService.LogAsync(
                    new AuditLogEntry(
                        AuditEventTypes.RefreshTokenReuseDetected,
                        AuditEntityTypes.User,
                        compromisedUser?.PublicId,
                        compromisedUser?.Email,
                        AuditActions.SecurityAlert,
                        $"Refresh token reuse detected for token family {storedToken.FamilyId}; the family was revoked."),
                    cancellationToken);

                await refreshTokenRepository.SaveChangesAsync(cancellationToken);

                logger.LogWarning(
                    "Refresh token reuse detected for family {RefreshTokenFamilyId}",
                    storedToken.FamilyId);
            }

            return Result<RefreshTokenExchangeResult>.Failure(AuthErrors.RefreshTokenInvalid);
        }

        var user = await userRepository.GetByIdAsync(storedToken.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            logger.LogWarning(
                "Refresh token rejected because user {UserId} is unavailable or inactive",
                storedToken.UserId);
            return Result<RefreshTokenExchangeResult>.Failure(AuthErrors.RefreshTokenInvalid);
        }

        var newRefreshTokenValue = CreateRefreshTokenValue();
        var newRefreshTokenHash = refreshTokenHasher.Hash(newRefreshTokenValue);
        var newRefreshToken = storedToken.Rotate(
            newRefreshTokenHash,
            utcNow.AddDays(jwtOptions.RefreshTokenExpirationDays),
            utcNow);

        await refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        var accessToken = await CreateAccessTokenAsync(user, jwtOptions, utcNow, clientType, cancellationToken);
        var accessTokenValue = new JwtSecurityTokenHandler().WriteToken(accessToken);
        var expiresIn = (int)Math.Round((accessToken.ValidTo - utcNow).TotalSeconds);

        logger.LogInformation(
            "Rotated refresh token for user {UserPublicId} family {RefreshTokenFamilyId} client type {ClientType} audience {Audience} key fingerprint {KeyFingerprint}",
            user.PublicId,
            storedToken.FamilyId,
            clientType,
            ResolveAudience(jwtOptions, clientType),
            JwtConfigurationDiagnostics.ComputeSigningKeyFingerprint(jwtOptions.SigningKey));

        return Result<RefreshTokenExchangeResult>.Success(new RefreshTokenExchangeResult(
            user,
            new AuthTokenResult(
                accessTokenValue,
                newRefreshTokenValue,
                expiresIn)));
    }

    private async Task<JwtSecurityToken> CreateAccessTokenAsync(
        User user,
        JwtTokenOptions jwtOptions,
        DateTime issuedAt,
        AuthClientType clientType,
        CancellationToken cancellationToken)
    {
        var expiresAt = issuedAt.AddMinutes(jwtOptions.AccessTokenExpirationMinutes);
        var tenantId = clientType == AuthClientType.Core
            ? await userCompanyRepository.GetActivePrimaryCompanyPublicIdAsync(user.Id, cancellationToken)
            : null;
        var claims = await CreateIdentityClaimsAsync(user, tenantId, clientType, includeAuthorizationClaims: true, cancellationToken);

        var signingCredentials = new SigningCredentials(
            JwtConfigurationDiagnostics.CreateSigningKey(jwtOptions.SigningKey!),
            SecurityAlgorithms.HmacSha256);

        return new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: ResolveAudience(jwtOptions, clientType),
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: signingCredentials);
    }

    private async Task<List<Claim>> CreateIdentityClaimsAsync(
        User user,
        Guid? tenantId,
        AuthClientType clientType,
        bool includeAuthorizationClaims,
        CancellationToken cancellationToken)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.PublicId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.PublicId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new("auth_provider", user.AuthProvider.ToString()),
            new("user_status", user.Status.ToString()),
            new("client_type", clientType.ToClaimValue())
        };

        if (!tenantId.HasValue)
        {
            return claims;
        }

        claims.Add(new Claim("tid", tenantId.Value.ToString()));

        var language = await userPreferenceRepository.ResolveLanguageAsync(user.Id, cancellationToken) ?? DefaultLanguage;
        claims.Add(new Claim(LanguageClaimType, language));

        if (!includeAuthorizationClaims)
        {
            return claims;
        }

        var iamUser = await iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
            tenantId.Value,
            user.PublicId,
            includeRoles: true,
            cancellationToken);
        if (iamUser is not null)
        {
            var roleClaims = iamUser.RoleAssignments
                .Select(static assignment => assignment.Role.NormalizedName)
                .Where(static role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static role => role, StringComparer.Ordinal);

            foreach (var role in roleClaims)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
            }

            var permissionClaims = iamUser.RoleAssignments
                .SelectMany(static assignment => assignment.Role.PermissionAssignments)
                .Select(static assignment => assignment.Permission.NormalizedCode)
                .Where(static permission => !string.IsNullOrWhiteSpace(permission))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static permission => permission, StringComparer.Ordinal);

            foreach (var permission in permissionClaims)
            {
                claims.Add(new Claim("permission", permission));
            }

            return claims;
        }

        var fallbackRole = await userCompanyRepository.GetRoleNormalizedNameAsync(user.Id, tenantId.Value, cancellationToken);
        if (!string.IsNullOrWhiteSpace(fallbackRole))
        {
            claims.Add(new Claim(ClaimTypes.Role, fallbackRole));
            claims.Add(new Claim("role", fallbackRole));
        }

        return claims;
    }

    private static string ResolveAudience(JwtTokenOptions jwtOptions, AuthClientType clientType) =>
        clientType switch
        {
            AuthClientType.Core => jwtOptions.Audience!,
            AuthClientType.Platform => jwtOptions.PlatformAudience!,
            _ => throw new ArgumentOutOfRangeException(nameof(clientType), clientType, "Unsupported auth client type.")
        };

    private static string CreateRefreshTokenValue() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
}
