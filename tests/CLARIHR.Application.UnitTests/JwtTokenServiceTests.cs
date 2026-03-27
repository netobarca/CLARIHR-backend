using System.Reflection;
using System.IdentityModel.Tokens.Jwt;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;
using CLARIHR.Infrastructure.Auth;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CLARIHR.Application.UnitTests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public async Task GenerateAsync_WhenConfigured_ShouldPersistRefreshTokenAndReturnTokenPair()
    {
        var userRepository = new TestUserRepository();
        var refreshTokenRepository = new TestRefreshTokenRepository();
        var primaryTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var userCompanyRepository = new TestUserCompanyRepository(
            primaryTenantId,
            new Dictionary<Guid, string>
            {
                [primaryTenantId] = "ADMIN DE EMPRESA"
            });
        var user = CreatePersistedUser();
        userRepository.Seed(user);

        var service = CreateService(userRepository, userCompanyRepository, refreshTokenRepository);

        var result = await service.GenerateAsync(user, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        Assert.Equal(900, result.Value.ExpiresIn);
        Assert.Single(refreshTokenRepository.Items);
        Assert.Equal(user.Id, refreshTokenRepository.Items[0].UserId);
        Assert.NotEqual(result.Value.RefreshToken, refreshTokenRepository.Items[0].TokenHash);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.AccessToken);
        Assert.Equal("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", jwt.Claims.Single(claim => claim.Type == "tid").Value);
        Assert.Equal("ADMIN DE EMPRESA", jwt.Claims.Single(claim => claim.Type == "role").Value);
    }

    [Fact]
    public async Task GenerateForTenantAsync_WhenTenantIsExplicit_ShouldUseTenantRoleClaim()
    {
        var userRepository = new TestUserRepository();
        var refreshTokenRepository = new TestRefreshTokenRepository();
        var primaryTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var targetTenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var userCompanyRepository = new TestUserCompanyRepository(
            primaryTenantId,
            new Dictionary<Guid, string>
            {
                [primaryTenantId] = "SECURITY OPERATOR",
                [targetTenantId] = "AUDITOR B"
            });
        var user = CreatePersistedUser();
        userRepository.Seed(user);

        var service = CreateService(userRepository, userCompanyRepository, refreshTokenRepository);

        var result = await service.GenerateForTenantAsync(user, targetTenantId, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.AccessToken);
        Assert.Equal(targetTenantId.ToString(), jwt.Claims.Single(claim => claim.Type == "tid").Value);
        Assert.Equal("AUDITOR B", jwt.Claims.Single(claim => claim.Type == "role").Value);
    }

    [Fact]
    public async Task RefreshAsync_WhenRefreshTokenIsActive_ShouldRotateRefreshToken()
    {
        var userRepository = new TestUserRepository();
        var refreshTokenRepository = new TestRefreshTokenRepository();
        var primaryTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var userCompanyRepository = new TestUserCompanyRepository(
            primaryTenantId,
            new Dictionary<Guid, string>
            {
                [primaryTenantId] = "ADMIN DE EMPRESA"
            });
        var user = CreatePersistedUser();
        userRepository.Seed(user);

        var service = CreateService(userRepository, userCompanyRepository, refreshTokenRepository);
        var issuedTokens = await service.GenerateAsync(user, CancellationToken.None);

        var result = await service.RefreshAsync(issuedTokens.Value.RefreshToken!, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(issuedTokens.Value.RefreshToken, result.Value.Tokens.RefreshToken);
        Assert.Equal(user.PublicId, result.Value.User.PublicId);
        Assert.Equal(2, refreshTokenRepository.Items.Count);
        Assert.Single(refreshTokenRepository.Items, token => token.RevokedUtc is null);
        Assert.Single(refreshTokenRepository.Items, token => token.RevokedUtc is not null);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.Tokens.AccessToken);
        Assert.Equal(primaryTenantId.ToString(), jwt.Claims.Single(claim => claim.Type == "tid").Value);
        Assert.Equal("ADMIN DE EMPRESA", jwt.Claims.Single(claim => claim.Type == "role").Value);
    }

    [Fact]
    public async Task RefreshAsync_WhenRotatedTokenIsReused_ShouldRevokeTokenFamily()
    {
        var userRepository = new TestUserRepository();
        var refreshTokenRepository = new TestRefreshTokenRepository();
        var primaryTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var userCompanyRepository = new TestUserCompanyRepository(
            primaryTenantId,
            new Dictionary<Guid, string>
            {
                [primaryTenantId] = "ADMIN DE EMPRESA"
            });
        var user = CreatePersistedUser();
        userRepository.Seed(user);

        var service = CreateService(userRepository, userCompanyRepository, refreshTokenRepository);
        var initialTokens = await service.GenerateAsync(user, CancellationToken.None);
        var rotatedTokens = await service.RefreshAsync(initialTokens.Value.RefreshToken!, CancellationToken.None);

        var reuseAttempt = await service.RefreshAsync(initialTokens.Value.RefreshToken!, CancellationToken.None);
        var secondTokenAttempt = await service.RefreshAsync(rotatedTokens.Value.Tokens.RefreshToken!, CancellationToken.None);

        Assert.True(reuseAttempt.IsFailure);
        Assert.Equal(AuthErrors.RefreshTokenInvalid.Code, reuseAttempt.Error.Code);
        Assert.True(secondTokenAttempt.IsFailure);
        Assert.Equal(AuthErrors.RefreshTokenInvalid.Code, secondTokenAttempt.Error.Code);
        Assert.All(refreshTokenRepository.Items, token => Assert.NotNull(token.RevokedUtc));
    }

    private static JwtTokenService CreateService(
        TestUserRepository userRepository,
        TestUserCompanyRepository userCompanyRepository,
        TestRefreshTokenRepository refreshTokenRepository) =>
        new(
            Options.Create(new JwtTokenOptions
            {
                Issuer = "https://issuer.test",
                Audience = "clarihr-api",
                SigningKey = "super-secret-signing-key-1234567890",
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 14
            }),
            new FixedDateTimeProvider(new DateTime(2026, 2, 28, 18, 0, 0, DateTimeKind.Utc)),
            userRepository,
            userCompanyRepository,
            refreshTokenRepository,
            new RefreshTokenHasher(),
            NullLogger<JwtTokenService>.Instance);

    private static User CreatePersistedUser()
    {
        var user = User.RegisterLocal(
            "Ana",
            "Mendoza",
            "ANA@Example.com ",
            "hashed-password",
            "SV",
            "landing");

        SetEntityId(user, 100);
        return user;
    }

    private static void SetEntityId(Entity entity, long id)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(entity, [id]);
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class TestUserRepository : IUserRepository
    {
        private readonly List<User> _items = [];

        public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.SingleOrDefault(user => user.Id == userId));

        public Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.SingleOrDefault(user => user.PublicId == userPublicId));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            var normalizedEmail = User.NormalizeEmail(email);
            return Task.FromResult(_items.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail));
        }

        public Task<User?> GetByExternalProviderAsync(AuthProvider authProvider, string providerUserId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.SingleOrDefault(user =>
                user.AuthProvider == authProvider &&
                user.ProviderUserId == providerUserId));

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            _items.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Seed(User user) => _items.Add(user);
    }

    private sealed class TestUserCompanyRepository(
        Guid? primaryCompanyPublicId,
        IReadOnlyDictionary<Guid, string> rolesByCompanyPublicId) : IUserCompanyRepository
    {
        public void Add(Domain.Companies.UserCompanyMembership membership) =>
            throw new NotSupportedException();

        public Task<bool> ExistsInCompanyAsync(Guid companyPublicId, string normalizedEmail, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasAnyMembershipAsync(long userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasPrimaryCompanyAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<Guid?> GetPrimaryCompanyPublicIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(primaryCompanyPublicId);

        public Task<Domain.Companies.UserCompanyMembership?> GetPrimaryMembershipAsync(long userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Domain.Companies.UserCompanyMembership?> GetMembershipAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> GetRoleNormalizedNameAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(rolesByCompanyPublicId.TryGetValue(companyPublicId, out var role) ? role : null);

        public Task<Domain.Companies.UserCompanyMembership?> FindByUserPublicIdAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> UserExistsOutsideCompanyAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> HasActiveMembershipAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetPrimaryCompanyAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsLastActiveAdministratorAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResponse<CompanyUserSummaryResponse>> GetUsersAsync(
            Guid companyPublicId,
            int pageNumber,
            int pageSize,
            UserStatus? status,
            Guid? roleId,
            string? search,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CompanyUserResponse?> GetUserAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly List<RefreshToken> _items = [];
        private long _nextId = 1;

        public IReadOnlyList<RefreshToken> Items => _items;

        public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(_items.SingleOrDefault(refreshToken => refreshToken.TokenHash == tokenHash));

        public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            SetEntityId(refreshToken, _nextId++);
            _items.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task RevokeFamilyAsync(Guid familyId, DateTime revokedUtc, string reason, CancellationToken cancellationToken)
        {
            foreach (var refreshToken in _items.Where(item => item.FamilyId == familyId && item.RevokedUtc is null))
            {
                refreshToken.Revoke(revokedUtc, reason);
            }

            return Task.CompletedTask;
        }

        public Task RevokeUserTokensAsync(long userId, DateTime revokedUtc, string reason, CancellationToken cancellationToken)
        {
            foreach (var refreshToken in _items.Where(item => item.UserId == userId && item.RevokedUtc is null))
            {
                refreshToken.Revoke(revokedUtc, reason);
            }

            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
