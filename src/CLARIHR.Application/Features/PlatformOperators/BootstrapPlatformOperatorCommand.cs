using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Platform.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Platform;
using FluentValidation;

namespace CLARIHR.Application.Features.PlatformOperators;

public sealed record BootstrapPlatformOperatorCommand(
    string Email,
    PlatformOperatorRole Role = PlatformOperatorRole.Admin) : ICommand<BootstrapPlatformOperatorResponse>;

public sealed record BootstrapPlatformOperatorResponse(
    Guid PlatformOperatorId,
    Guid UserId,
    string Email,
    PlatformOperatorRole Role);

internal sealed class BootstrapPlatformOperatorCommandValidator : AbstractValidator<BootstrapPlatformOperatorCommand>
{
    public BootstrapPlatformOperatorCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(command => command.Role)
            .IsInEnum();
    }
}

internal sealed class BootstrapPlatformOperatorCommandHandler(
    IUserRepository userRepository,
    IPlatformOperatorRepository platformOperatorRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<BootstrapPlatformOperatorCommand, BootstrapPlatformOperatorResponse>
{
    public async Task<Result<BootstrapPlatformOperatorResponse>> Handle(
        BootstrapPlatformOperatorCommand command,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (user is null)
        {
            return Result<BootstrapPlatformOperatorResponse>.Failure(PlatformOperatorErrors.UserNotFound);
        }

        if (user.Status != UserStatus.Active ||
            user.AuthProvider != AuthProvider.Local ||
            string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return Result<BootstrapPlatformOperatorResponse>.Failure(PlatformOperatorErrors.UserMustBeActiveLocal);
        }

        if (await platformOperatorRepository.GetByUserIdAsync(user.Id, cancellationToken) is not null)
        {
            return Result<BootstrapPlatformOperatorResponse>.Failure(PlatformOperatorErrors.AlreadyExists);
        }

        var platformOperator = PlatformOperator.Create(user.Id, command.Role);
        platformOperatorRepository.Add(platformOperator);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BootstrapPlatformOperatorResponse>.Success(new BootstrapPlatformOperatorResponse(
            platformOperator.PublicId,
            user.PublicId,
            user.Email,
            platformOperator.Role));
    }
}
