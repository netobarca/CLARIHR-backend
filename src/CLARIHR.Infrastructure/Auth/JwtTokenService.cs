using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Domain.Auth;
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
    ICompanySubscriptionRepository companySubscriptionRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IRefreshTokenHasher refreshTokenHasher,
    ILogger<JwtTokenService> logger) : ITokenService
{
    public async Task<Result<AuthTokenResult>> GenerateAsync(User user, CancellationToken cancellationToken)
    {
        var tenantId = await userCompanyRepository.GetPrimaryCompanyPublicIdAsync(user.Id, cancellationToken);
        return await GenerateInternalAsync(user, tenantId, cancellationToken);
    }

    public Task<Result<AuthTokenResult>> GenerateForTenantAsync(User user, Guid tenantId, CancellationToken cancellationToken) =>
        GenerateInternalAsync(user, tenantId, cancellationToken);

    private async Task<Result<AuthTokenResult>> GenerateInternalAsync(
        User user,
        Guid? tenantId,
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
            new("user_status", user.Status.ToString())
        };

        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tid", tenantId.Value.ToString()));

            var planCode = await companySubscriptionRepository.GetActivePlanCodeAsync(
                tenantId.Value, cancellationToken);
            if (planCode is not null)
            {
                claims.Add(new Claim("plan_code", planCode));
            }
        }

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey!)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        var serializedToken = new JwtSecurityTokenHandler().WriteToken(token);
        var rawRefreshToken = CreateRefreshTokenValue();
        var refreshToken = RefreshToken.Issue(
            user.Id,
            refreshTokenHasher.Hash(rawRefreshToken),
            refreshExpiresAt);

        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        var expiresIn = (int)Math.Round((expiresAt - issuedAt).TotalSeconds);
        logger.LogInformation(
            "Issued auth token pair for user {UserPublicId} tenant {TenantId} provider {AuthProvider}",
            user.PublicId,
            tenantId,
            user.AuthProvider);

        return Result<AuthTokenResult>.Success(new AuthTokenResult(
            serializedToken,
            RefreshToken: rawRefreshToken,
            ExpiresIn: expiresIn));
    }

    public async Task<Result<RefreshTokenExchangeResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
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

        if (!storedToken.IsActive(utcNow))
        {
            if (storedToken.HasBeenRotated)
            {
                await refreshTokenRepository.RevokeFamilyAsync(
                    storedToken.FamilyId,
                    utcNow,
                    "reuse-detected",
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

        var accessToken = await CreateAccessTokenAsync(user, jwtOptions, utcNow, cancellationToken);
        var accessTokenValue = new JwtSecurityTokenHandler().WriteToken(accessToken);
        var expiresIn = (int)Math.Round((accessToken.ValidTo - utcNow).TotalSeconds);

        logger.LogInformation(
            "Rotated refresh token for user {UserPublicId} family {RefreshTokenFamilyId}",
            user.PublicId,
            storedToken.FamilyId);

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
        CancellationToken cancellationToken)
    {
        var expiresAt = issuedAt.AddMinutes(jwtOptions.AccessTokenExpirationMinutes);
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
            new("user_status", user.Status.ToString())
        };

        var tenantId = await userCompanyRepository.GetPrimaryCompanyPublicIdAsync(user.Id, cancellationToken);
        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tid", tenantId.Value.ToString()));

            var planCode = await companySubscriptionRepository.GetActivePlanCodeAsync(
                tenantId.Value, cancellationToken);
            if (planCode is not null)
            {
                claims.Add(new Claim("plan_code", planCode));
            }
        }

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey!)),
            SecurityAlgorithms.HmacSha256);

        return new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: signingCredentials);
    }

    private static string CreateRefreshTokenValue() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
}
