using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Preferences;
using FluentValidation;

namespace CLARIHR.Application.Features.Auth.External;

public sealed record RegisterExternalUserCommand(
    AuthProvider Provider,
    string IdToken,
    string? Country,
    string? Source) : ICommand<ExternalAuthCommandResult>;

internal sealed class RegisterExternalUserCommandValidator : AbstractValidator<RegisterExternalUserCommand>
{
    public RegisterExternalUserCommandValidator()
    {
        RuleFor(command => command.Provider)
            .Must(static provider => provider != AuthProvider.Local)
            .WithMessage("An external provider is required.");

        RuleFor(command => command.IdToken)
            .NotEmpty()
            .MaximumLength(8000);

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

internal sealed class RegisterExternalUserCommandHandler(
    IUserRepository userRepository,
    IUserPreferenceRepository userPreferenceRepository,
    IExternalAuthProviderService externalAuthProviderService,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : ICommandHandler<RegisterExternalUserCommand, ExternalAuthCommandResult>
{
    public async Task<Result<ExternalAuthCommandResult>> Handle(
        RegisterExternalUserCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var validationResult = await externalAuthProviderService.ValidateAsync(
            command.Provider,
            command.IdToken,
            cancellationToken);

        if (validationResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ExternalAuthCommandResult>.Failure(validationResult.Error);
        }

        var externalUser = validationResult.Value;
        if (string.IsNullOrWhiteSpace(externalUser.Email))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ExternalAuthCommandResult>.Failure(AuthErrors.ExternalEmailMissing);
        }

        var user = await userRepository.GetByExternalProviderAsync(
            externalUser.Provider,
            externalUser.ProviderUserId,
            cancellationToken);

        var wasCreated = false;
        if (user is null)
        {
            user = await userRepository.GetByEmailAsync(externalUser.Email, cancellationToken);
        }

        if (user is null)
        {
            user = User.RegisterExternal(
                externalUser.FirstName,
                externalUser.LastName,
                externalUser.Email,
                externalUser.Provider,
                externalUser.ProviderUserId,
                command.Country,
                command.Source);

            await userRepository.AddAsync(user, cancellationToken);
            wasCreated = true;
        }
        else if (user is { Status: UserStatus.PendingEmailVerification, ProviderUserId: null } &&
                 externalUser.CanAutoLinkByEmail)
        {
            // AU-1: a verified external identity (proven email ownership) claims a still-pending, unverified
            // local account squatting this email — activate it, link the provider, and discard the never-
            // verified password so the original (unverified) registrant cannot retain access.
            user.ActivateAsExternal(externalUser.Provider, externalUser.ProviderUserId);
        }
        else
        {
            if (user.Status != UserStatus.Active)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<ExternalAuthCommandResult>.Failure(AuthErrors.UserNotActive);
            }

            if (user.ProviderUserId is null && !externalUser.CanAutoLinkByEmail)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<ExternalAuthCommandResult>.Failure(AuthErrors.ExternalEmailLinkNotAllowed);
            }

            try
            {
                user.EnsureExternalProviderLink(externalUser.Provider, externalUser.ProviderUserId);
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<ExternalAuthCommandResult>.Failure(AuthErrors.ExternalProviderLinkConflict);
            }
        }

        await userRepository.SaveChangesAsync(cancellationToken);

        if (wasCreated)
        {
            userPreferenceRepository.Add(UserPreference.Create(user.Id));
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var tokenResult = await tokenService.GenerateAsync(user, cancellationToken);
        if (tokenResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ExternalAuthCommandResult>.Failure(tokenResult.Error);
        }

        await transaction.CommitAsync(cancellationToken);

        var response = new AuthResponse(
            tokenResult.Value.AccessToken,
            tokenResult.Value.RefreshToken,
            tokenResult.Value.ExpiresIn,
            new UserDto(
                user.PublicId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.AuthProvider));

        return Result<ExternalAuthCommandResult>.Success(new ExternalAuthCommandResult(response, wasCreated));
    }
}
