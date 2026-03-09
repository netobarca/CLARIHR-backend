using CLARIHR.Domain.Auth;
using CLARIHR.Application.Features.Auth.RegisterUser;

namespace CLARIHR.Application.Features.Auth.External;

public sealed record RegisterExternalRequest(
    AuthProvider Provider,
    string IdToken,
    string? Country,
    string? Source);

public sealed record ExternalAuthCommandResult(
    AuthResponse Response,
    bool WasCreated);
