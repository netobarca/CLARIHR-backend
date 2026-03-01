namespace CLARIHR.Application.Abstractions.Auth;

public sealed record AuthTokenResult(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn);
