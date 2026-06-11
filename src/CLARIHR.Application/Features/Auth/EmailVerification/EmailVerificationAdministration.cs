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

namespace CLARIHR.Application.Features.Auth.EmailVerification;

public sealed record ConfirmEmailVerificationCommand(string Token) : ICommand<AuthResponse>;

public sealed record ResendEmailVerificationCommand(string Email) : ICommand<bool>;

internal sealed class ConfirmEmailVerificationCommandValidator : AbstractValidator<ConfirmEmailVerificationCommand>
{
    public ConfirmEmailVerificationCommandValidator()
    {
        RuleFor(command => command.Token)
            .NotEmpty()
            .MaximumLength(500);
    }
}

internal sealed class ResendEmailVerificationCommandValidator : AbstractValidator<ResendEmailVerificationCommand>
{
    public ResendEmailVerificationCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
    }
}

internal sealed class ConfirmEmailVerificationCommandHandler(
    IUserRepository userRepository,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    IEmailVerificationTokenHasher emailVerificationTokenHasher,
    ITokenService tokenService,
    IPlatformAuditService platformAuditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ConfirmEmailVerificationCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(ConfirmEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var resolution = await emailVerificationTokenRepository.GetActiveByHashAsync(
            emailVerificationTokenHasher.Hash(command.Token),
            utcNow,
            cancellationToken);
        if (resolution is null)
        {
            return Result<AuthResponse>.Failure(AuthErrors.EmailVerificationTokenInvalid);
        }

        var user = await userRepository.GetByIdAsync(resolution.Token.UserId, cancellationToken);
        if (user is null ||
            user.AuthProvider != AuthProvider.Local ||
            user.Status != UserStatus.PendingEmailVerification)
        {
            return Result<AuthResponse>.Failure(AuthErrors.EmailVerificationTokenInvalid);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            user.ConfirmEmail();
            resolution.Token.MarkUsed(utcNow);
            await emailVerificationTokenRepository.RevokeActiveTokensAsync(user.Id, utcNow, cancellationToken);

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.UserEmailVerified,
                    AuditEntityTypes.User,
                    user.PublicId,
                    user.Email,
                    AuditActions.Update,
                    $"Email verified for user {user.Email}."),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var tokenResult = await tokenService.GenerateAsync(user, cancellationToken);
            if (tokenResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<AuthResponse>.Failure(tokenResult.Error);
            }

            await transaction.CommitAsync(cancellationToken);

            return Result<AuthResponse>.Success(
                new AuthResponse(
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
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ResendEmailVerificationCommandHandler(
    IUserRepository userRepository,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    IEmailVerificationTokenHasher emailVerificationTokenHasher,
    IEmailVerificationTokenGenerator emailVerificationTokenGenerator,
    IAuthEmailService authEmailService,
    IEmailVerificationLinkBuilder emailVerificationLinkBuilder,
    IEmailVerificationPolicyProvider emailVerificationPolicyProvider,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ResendEmailVerificationCommand, bool>
{
    public async Task<Result<bool>> Handle(ResendEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (user is null ||
            user.AuthProvider != AuthProvider.Local ||
            user.Status != UserStatus.PendingEmailVerification)
        {
            // Uniform success regardless of account state (anti-enumeration).
            return Result<bool>.Success(true);
        }

        var utcNow = dateTimeProvider.UtcNow;
        var cooldownUtc = utcNow.AddMinutes(-emailVerificationPolicyProvider.GetCooldownMinutes());
        if (await emailVerificationTokenRepository.HasRecentRequestAsync(user.Id, cooldownUtc, cancellationToken))
        {
            return Result<bool>.Success(true);
        }

        var message = await EmailVerificationDispatch.StageAsync(
            user,
            emailVerificationTokenRepository,
            emailVerificationTokenHasher,
            emailVerificationTokenGenerator,
            emailVerificationLinkBuilder,
            emailVerificationPolicyProvider,
            utcNow,
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        await authEmailService.SendEmailVerificationAsync(message, cancellationToken);

        return Result<bool>.Success(true);
    }
}

// Single source of the security-sensitive verification-token issuance: revoke any active token for the user
// and stage a fresh single-use token, returning the email to send AFTER the caller commits. Shared by the
// registration handler and the resend handler so the two cannot drift.
internal static class EmailVerificationDispatch
{
    public static async Task<EmailVerificationEmailMessage> StageAsync(
        User user,
        IEmailVerificationTokenRepository tokenRepository,
        IEmailVerificationTokenHasher tokenHasher,
        IEmailVerificationTokenGenerator tokenGenerator,
        IEmailVerificationLinkBuilder linkBuilder,
        IEmailVerificationPolicyProvider policyProvider,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var rawToken = tokenGenerator.Generate();
        var expirationUtc = utcNow.AddMinutes(policyProvider.GetTokenLifetimeMinutes());

        await tokenRepository.RevokeActiveTokensAsync(user.Id, utcNow, cancellationToken);
        tokenRepository.Add(EmailVerificationToken.Issue(
            user.Id,
            tokenHasher.Hash(rawToken),
            expirationUtc));

        return new EmailVerificationEmailMessage(
            user.Email,
            user.FirstName,
            user.LastName,
            linkBuilder.Build(rawToken),
            expirationUtc);
    }
}
