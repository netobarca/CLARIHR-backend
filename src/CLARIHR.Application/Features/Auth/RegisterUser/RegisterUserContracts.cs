using CLARIHR.Domain.Auth;
using CLARIHR.Application.Features.LegalRepresentatives.Common;

namespace CLARIHR.Application.Features.Auth.RegisterUser;

public sealed record RegisterUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? CompanyName,
    InitialLegalRepresentativeInput InitialLegalRepresentative,
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
