using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.RefreshToken;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.UnitTests;

public sealed class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenRefreshTokenIsEmpty_ShouldReturnValidationError()
    {
        var result = _validator.Validate(new RefreshTokenCommand(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RefreshTokenCommand.RefreshToken));
    }
}

public sealed class RefreshTokenCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenRefreshTokenIsInvalid_ShouldReturnUnauthorized()
    {
        var handler = new RefreshTokenCommandHandler(new FailingTokenService());

        var result = await handler.Handle(new RefreshTokenCommand("invalid-refresh-token"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal(AuthErrors.RefreshTokenInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenIsValid_ShouldReturnRotatedTokenPair()
    {
        var user = User.RegisterLocal(
            "Ana",
            "Mendoza",
            "ANA@Example.com ",
            "hashed-password",
            "SV",
            "landing");

        SetEntityId(user, 10);

        var handler = new RefreshTokenCommandHandler(new SuccessfulTokenService(user));

        var result = await handler.Handle(new RefreshTokenCommand("refresh-token"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-access-token", result.Value.AccessToken);
        Assert.Equal("new-refresh-token", result.Value.RefreshToken);
        Assert.Equal("ana@example.com", result.Value.User.Email);
        Assert.Equal(AuthProvider.Local, result.Value.User.AuthProvider);
    }

    private static void SetEntityId(Domain.Common.Entity entity, long id)
    {
        typeof(Domain.Common.Entity)
            .GetProperty(nameof(Domain.Common.Entity.Id), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(entity, [id]);
    }

    private sealed class SuccessfulTokenService(User user) : ITokenService
    {
        public Task<Result<AuthTokenResult>> GenerateAsync(User user, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<RefreshTokenExchangeResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
            Task.FromResult(Result<RefreshTokenExchangeResult>.Success(new RefreshTokenExchangeResult(
                user,
                new AuthTokenResult("new-access-token", "new-refresh-token", 900))));
    }

    private sealed class FailingTokenService : ITokenService
    {
        public Task<Result<AuthTokenResult>> GenerateAsync(User user, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<RefreshTokenExchangeResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
            Task.FromResult(Result<RefreshTokenExchangeResult>.Failure(AuthErrors.RefreshTokenInvalid));
    }
}
