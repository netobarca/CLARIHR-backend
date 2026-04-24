using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Domain.Auth;
using FluentValidation;

namespace CLARIHR.Application.Features.Auth.PasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : ICommand<bool>;

public sealed record ValidatePasswordResetTokenQuery(string Token) : IQuery<PasswordResetTokenValidationResponse>;

public sealed record RedeemPasswordResetCommand(string Token, string NewPassword) : ICommand<bool>;

public sealed record PasswordResetTokenValidationResponse(DateTime ExpiresAtUtc, string MaskedEmail);

internal sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
    }
}

internal sealed class ValidatePasswordResetTokenQueryValidator : AbstractValidator<ValidatePasswordResetTokenQuery>
{
    public ValidatePasswordResetTokenQueryValidator()
    {
        RuleFor(query => query.Token)
            .NotEmpty()
            .MaximumLength(500);
    }
}

internal sealed class RedeemPasswordResetCommandValidator : AbstractValidator<RedeemPasswordResetCommand>
{
    public RedeemPasswordResetCommandValidator()
    {
        RuleFor(command => command.Token)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(command => command.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Custom((password, context) =>
            {
                foreach (var error in AuthValidationRules.GetPasswordPolicyViolations(
                             password,
                             firstName: null,
                             lastName: null,
                             email: null))
                {
                    context.AddFailure(nameof(RedeemPasswordResetCommand.NewPassword), error);
                }
            });
    }
}

internal sealed class RequestPasswordResetCommandHandler(
    IUserRepository userRepository,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordResetTokenHasher passwordResetTokenHasher,
    IPasswordResetTokenGenerator passwordResetTokenGenerator,
    IAuthEmailService authEmailService,
    IDateTimeProvider dateTimeProvider,
    IPasswordResetLinkBuilder passwordResetLinkBuilder,
    IPasswordResetPolicyProvider passwordResetPolicyProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RequestPasswordResetCommand, bool>
{
    public async Task<Result<bool>> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (user is null ||
            user.AuthProvider != AuthProvider.Local ||
            user.Status != UserStatus.Active)
        {
            return Result<bool>.Success(true);
        }

        var utcNow = dateTimeProvider.UtcNow;
        var cooldownUtc = utcNow.AddMinutes(-passwordResetPolicyProvider.GetCooldownMinutes());
        if (await passwordResetTokenRepository.HasRecentRequestAsync(user.Id, cooldownUtc, cancellationToken))
        {
            return Result<bool>.Success(true);
        }

        var rawToken = passwordResetTokenGenerator.Generate();
        var expirationUtc = utcNow.AddMinutes(passwordResetPolicyProvider.GetTokenLifetimeMinutes());

        await passwordResetTokenRepository.RevokeActiveTokensAsync(user.Id, utcNow, cancellationToken);
        passwordResetTokenRepository.Add(PasswordResetToken.Issue(
            user.Id,
            passwordResetTokenHasher.Hash(rawToken),
            expirationUtc));

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserUpdated,
                AuditEntityTypes.User,
                user.PublicId,
                user.Email,
                AuditActions.Update,
                $"Password reset requested for user {user.Email}.",
                After: new
                {
                    userId = user.PublicId,
                    email = user.Email,
                    expirationUtc
                }),
            cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        await authEmailService.SendPasswordResetAsync(
            new PasswordResetEmailMessage(
                user.Email,
                user.FirstName,
                user.LastName,
                passwordResetLinkBuilder.Build(rawToken),
                expirationUtc),
            cancellationToken);

        return Result<bool>.Success(true);
    }
}

internal sealed class ValidatePasswordResetTokenQueryHandler(
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordResetTokenHasher passwordResetTokenHasher,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<ValidatePasswordResetTokenQuery, PasswordResetTokenValidationResponse>
{
    public async Task<Result<PasswordResetTokenValidationResponse>> Handle(
        ValidatePasswordResetTokenQuery query,
        CancellationToken cancellationToken)
    {
        var resolution = await passwordResetTokenRepository.GetActiveByHashAsync(
            passwordResetTokenHasher.Hash(query.Token),
            dateTimeProvider.UtcNow,
            cancellationToken);
        if (resolution is null)
        {
            return Result<PasswordResetTokenValidationResponse>.Failure(AuthErrors.PasswordResetTokenInvalid);
        }

        return Result<PasswordResetTokenValidationResponse>.Success(
            new PasswordResetTokenValidationResponse(
                resolution.Token.ExpirationUtc,
                PasswordResetHelpers.MaskEmail(resolution.Email)));
    }
}

internal sealed class RedeemPasswordResetCommandHandler(
    IUserRepository userRepository,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordResetTokenHasher passwordResetTokenHasher,
    IPasswordHasher passwordHasher,
    IRefreshTokenRepository refreshTokenRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RedeemPasswordResetCommand, bool>
{
    public async Task<Result<bool>> Handle(RedeemPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var resolution = await passwordResetTokenRepository.GetActiveByHashAsync(
            passwordResetTokenHasher.Hash(command.Token),
            utcNow,
            cancellationToken);
        if (resolution is null)
        {
            return Result<bool>.Failure(AuthErrors.PasswordResetTokenInvalid);
        }

        var user = await userRepository.GetByIdAsync(resolution.Token.UserId, cancellationToken);
        if (user is null ||
            user.AuthProvider != AuthProvider.Local ||
            user.Status != UserStatus.Active)
        {
            return Result<bool>.Failure(AuthErrors.PasswordResetTokenInvalid);
        }

        var policyViolations = AuthValidationRules.GetPasswordPolicyViolations(
                command.NewPassword,
                user.FirstName,
                user.LastName,
                user.Email)
            .ToArray();
        if (policyViolations.Length > 0)
        {
            return Result<bool>.Failure(new Error(
                "auth.password_reset.password_policy_violation",
                "The new password does not meet the password policy.",
                ErrorType.Validation,
                new Dictionary<string, string[]>
                {
                    [nameof(RedeemPasswordResetCommand.NewPassword)] = policyViolations
                }));
        }

        user.ResetLocalPassword(passwordHasher.Hash(command.NewPassword));
        resolution.Token.MarkUsed(utcNow);
        await passwordResetTokenRepository.RevokeActiveTokensAsync(user.Id, utcNow, cancellationToken);
        await refreshTokenRepository.RevokeUserTokensAsync(user.Id, AuthClientType.Core, utcNow, "password-reset", cancellationToken);
        await refreshTokenRepository.RevokeUserTokensAsync(user.Id, AuthClientType.Platform, utcNow, "password-reset", cancellationToken);

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserUpdated,
                AuditEntityTypes.User,
                user.PublicId,
                user.Email,
                AuditActions.Update,
                $"Password reset redeemed for user {user.Email}."),
            cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}

internal static class PasswordResetHelpers
{
    public static string MaskEmail(string email)
    {
        var normalizedEmail = User.NormalizeEmail(email);
        var atIndex = normalizedEmail.IndexOf('@');
        if (atIndex <= 1)
        {
            return "***";
        }

        var localPart = normalizedEmail[..atIndex];
        var domain = normalizedEmail[(atIndex + 1)..];
        var maskedLocal = localPart.Length <= 2
            ? $"{localPart[0]}*"
            : $"{localPart[0]}{new string('*', localPart.Length - 2)}{localPart[^1]}";

        return $"{maskedLocal}@{domain}";
    }
}
