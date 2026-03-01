using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Features.Auth.RegisterUser;

public sealed record RegisterUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? CompanyName,
    string? Country,
    string? Source);

public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    AuthProvider AuthProvider);

public sealed record AuthResponse(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    UserDto User);
