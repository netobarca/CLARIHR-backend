using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Auth;

public sealed record RefreshTokenExchangeResult(
    User User,
    AuthTokenResult Tokens);
