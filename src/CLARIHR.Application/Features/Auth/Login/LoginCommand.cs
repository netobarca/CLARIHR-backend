using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Domain.Auth;
using FluentValidation;

namespace CLARIHR.Application.Features.Auth.Login;

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record LoginCommand(
    string Email,
    string Password) : ICommand<AuthResponse>;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(100);
    }
}

internal sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IPlatformAuditService platformAuditService,
    ILoginThrottlePolicyProvider loginThrottlePolicy,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork) : ICommandHandler<LoginCommand, AuthResponse>
{
    // AU-5: a fixed dummy hash, computed once (lazily) via the injected hasher, lets login always pay one
    // full password verification — even for a non-existent / inactive / external account — so the response
    // time does not reveal whether the email maps to an active local account (timing-based enumeration).
    private const string TimingEqualizationPassword = "timing-equalization-placeholder-not-a-credential";
    private static string? _dummyPasswordHash;

    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        var utcNow = dateTimeProvider.UtcNow;

        // AU-5: always run exactly one PBKDF2 verification regardless of account state (timing equalization).
        // A non-eligible account (missing / inactive / external / no password) is checked against the dummy
        // hash so the cryptographic cost — and therefore the response time — is identical on every path.
        var comparisonHash = user is { AuthProvider: AuthProvider.Local, PasswordHash: { Length: > 0 } passwordHash }
            ? passwordHash
            : _dummyPasswordHash ??= passwordHasher.Hash(TimingEqualizationPassword);
        var passwordMatches = passwordHasher.Verify(command.Password, comparisonHash);

        var isEligible = user is { Status: UserStatus.Active, AuthProvider: AuthProvider.Local };
        var isLockedOut = user is not null && user.IsLockedOut(utcNow);

        if (!isEligible || isLockedOut || !passwordMatches)
        {
            // AU-4: count a failed attempt ONLY for a real eligible account giving a wrong password while not
            // already locked (don't penalize nonexistent/inactive accounts, and don't re-count during lockout).
            var justLocked = false;
            if (isEligible && !isLockedOut && !passwordMatches)
            {
                user!.RegisterFailedLogin(
                    utcNow,
                    loginThrottlePolicy.GetMaxFailedAttempts(),
                    loginThrottlePolicy.GetWindow(),
                    loginThrottlePolicy.GetLockoutDuration());
                justLocked = user.IsLockedOut(utcNow);
            }

            // AU-3: uniform failure record (brute-force detection); a tripped lockout is a distinct, higher-
            // signal event. No secrets — the attempted email is in the summary.
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    justLocked ? AuditEventTypes.UserLoginThrottled : AuditEventTypes.UserLoginFailed,
                    AuditEntityTypes.User,
                    user?.PublicId,
                    null,
                    AuditActions.Login,
                    justLocked
                        ? $"Account {command.Email} temporarily locked after repeated failed logins."
                        : $"Failed login attempt for {command.Email}."),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<AuthResponse>.Failure(AuthErrors.InvalidCredentials);
        }

        // Successful authentication clears any accumulated failure / lockout state (persisted with the token).
        user!.ResetFailedLogins();

        // AU-3: staged before token issuance so it is persisted atomically with the refresh token + the reset
        // by GenerateLoginAsync's SaveChanges.
        await platformAuditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserLoggedIn,
                AuditEntityTypes.User,
                user.PublicId,
                user.Email,
                AuditActions.Login,
                $"User {user.Email} logged in."),
            cancellationToken);

        var tokenResult = await tokenService.GenerateLoginAsync(user, cancellationToken);
        if (tokenResult.IsFailure)
        {
            return Result<AuthResponse>.Failure(tokenResult.Error);
        }

        return Result<AuthResponse>.Success(new AuthResponse(
            tokenResult.Value.AccessToken,
            tokenResult.Value.RefreshToken,
            tokenResult.Value.ExpiresIn,
            new UserDto(
                user.PublicId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.AuthProvider)));
    }
}
