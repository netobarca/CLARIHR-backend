using CLARIHR.Domain.Auth;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Application.Features.LegalRepresentatives.Common;

namespace CLARIHR.Application.Features.Auth.External;

public sealed record RegisterExternalRequest(
    AuthProvider Provider,
    string IdToken,
    string? CompanyName,
    InitialLegalRepresentativeInput? InitialLegalRepresentative,
    string? Country,
    string? Source);

public sealed record ExternalAuthCommandResult(
    AuthResponse Response,
    bool WasCreated);
