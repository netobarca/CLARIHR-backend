using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.EmailVerification;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Preferences;
using FluentValidation;

namespace CLARIHR.Application.Features.Auth.RegisterUser;

public sealed record RegisterUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? Country,
    string? Source) : ICommand<bool>;

internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(AuthValidationRules.BeValidPersonName)
            .WithMessage("First name contains invalid characters.");

        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(AuthValidationRules.BeValidPersonName)
            .WithMessage("Last name contains invalid characters.");

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Custom((password, context) =>
            {
                var command = (RegisterUserCommand)context.InstanceToValidate;
                foreach (var error in AuthValidationRules.GetPasswordPolicyViolations(
                             password,
                             command.FirstName,
                             command.LastName,
                             command.Email))
                {
                    context.AddFailure(nameof(RegisterUserCommand.Password), error);
                }
            });

        RuleFor(command => command.Country)
            .MaximumLength(100)
            .Must(AuthValidationRules.BeValidCountry)
            .WithMessage("Country contains invalid characters.")
            .When(static command => !string.IsNullOrWhiteSpace(command.Country));

        RuleFor(command => command.Source)
            .MaximumLength(100)
            .Must(AuthValidationRules.BeValidSource)
            .WithMessage("Source contains invalid characters.")
            .When(static command => !string.IsNullOrWhiteSpace(command.Source));
    }
}

// AU-1: registration no longer mints a session. It creates a NON-usable local account
// (PendingEmailVerification) and emails a single-use verification link; the account becomes Active — and a
// session is issued — only once the link is redeemed (ConfirmEmailVerificationCommand). This proves the
// registrant controls the email, closing the federated pre-account-hijacking vector, and the uniform success
// response (regardless of whether the email already exists) closes account enumeration.
internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IUserPreferenceRepository userPreferenceRepository,
    IPasswordHasher passwordHasher,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    IEmailVerificationTokenHasher emailVerificationTokenHasher,
    IEmailVerificationTokenGenerator emailVerificationTokenGenerator,
    IAuthEmailService authEmailService,
    IEmailVerificationLinkBuilder emailVerificationLinkBuilder,
    IEmailVerificationPolicyProvider emailVerificationPolicyProvider,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork) : ICommandHandler<RegisterUserCommand, bool>
{
    public async Task<Result<bool>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var existingUser = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (existingUser is not null)
        {
            // Uniform success regardless of whether the email exists (anti-enumeration). Only a still-pending
            // local account is re-notified: reissue + resend the verification (no password change — the link
            // always lands in the real mailbox, so only its owner can complete it), honoring the cooldown.
            if (existingUser is { AuthProvider: AuthProvider.Local, Status: UserStatus.PendingEmailVerification })
            {
                await ReissueVerificationIfAllowedAsync(existingUser, cancellationToken);
            }

            return Result<bool>.Success(true);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var user = User.RegisterLocalPendingVerification(
            command.FirstName,
            command.LastName,
            command.Email,
            passwordHasher.Hash(command.Password),
            command.Country,
            command.Source);

        await userRepository.AddAsync(user, cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        userPreferenceRepository.Add(UserPreference.Create(user.Id));
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var message = await EmailVerificationDispatch.StageAsync(
            user,
            emailVerificationTokenRepository,
            emailVerificationTokenHasher,
            emailVerificationTokenGenerator,
            emailVerificationLinkBuilder,
            emailVerificationPolicyProvider,
            dateTimeProvider.UtcNow,
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        await authEmailService.SendEmailVerificationAsync(message, cancellationToken);

        return Result<bool>.Success(true);
    }

    private async Task ReissueVerificationIfAllowedAsync(User user, CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var cooldownUtc = utcNow.AddMinutes(-emailVerificationPolicyProvider.GetCooldownMinutes());
        if (await emailVerificationTokenRepository.HasRecentRequestAsync(user.Id, cooldownUtc, cancellationToken))
        {
            return;
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
    }
}
