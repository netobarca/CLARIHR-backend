using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Auth.RegisterUser;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using FluentValidation;

namespace CLARIHR.Application.Features.Auth.AcceptInvitation;

public sealed record AcceptCompanyUserInvitationCommand(
    string Token,
    string Password) : ICommand<AuthResponse>;

internal sealed class AcceptCompanyUserInvitationCommandValidator : AbstractValidator<AcceptCompanyUserInvitationCommand>
{
    public AcceptCompanyUserInvitationCommandValidator()
    {
        RuleFor(command => command.Token)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(command => command.Password)
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
                    context.AddFailure(nameof(AcceptCompanyUserInvitationCommand.Password), error);
                }
            });
    }
}

internal sealed class AcceptCompanyUserInvitationCommandHandler(
    IUserRepository userRepository,
    IUserCompanyRepository userCompanyRepository,
    ICompanyRepository companyRepository,
    IIamAdministrationRepository iamAdministrationRepository,
    IInvitationTokenRepository invitationTokenRepository,
    IInvitationTokenHasher invitationTokenHasher,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AcceptCompanyUserInvitationCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(
        AcceptCompanyUserInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var resolution = await invitationTokenRepository.GetActiveByHashAsync(
            invitationTokenHasher.Hash(command.Token),
            dateTimeProvider.UtcNow,
            cancellationToken);
        if (resolution is null)
        {
            return Result<AuthResponse>.Failure(AuthErrors.InvitationTokenInvalid);
        }

        var user = await userRepository.GetByIdAsync(resolution.Token.UserId, cancellationToken);
        if (user is null || user.AuthProvider != AuthProvider.Local)
        {
            return Result<AuthResponse>.Failure(AuthErrors.InvitationTokenInvalid);
        }

        var membership = await userCompanyRepository.GetMembershipAsync(user.Id, resolution.CompanyPublicId, cancellationToken);
        if (membership is null)
        {
            return Result<AuthResponse>.Failure(AuthErrors.InvitationTokenInvalid);
        }

        // AC-1: never mint a session against an archived company, even on a still-valid invitation.
        var company = await companyRepository.FindByPublicIdAsync(resolution.CompanyPublicId, cancellationToken);
        if (company is null || company.Status != CompanyStatus.Active)
        {
            return Result<AuthResponse>.Failure(AuthErrors.InvitationCompanyUnavailable);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            user.CompleteLocalActivation(passwordHasher.Hash(command.Password));
            membership.Reactivate();
            resolution.Token.MarkUsed();

            var iamUser = await iamAdministrationRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
                resolution.CompanyPublicId,
                user.PublicId,
                includeRoles: true,
                cancellationToken);
            iamUser?.SetActive(true);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.UserActivated,
                    AuditEntityTypes.User,
                    user.PublicId,
                    user.Email,
                    AuditActions.Update,
                    $"Activated invited user {user.Email}."),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var tokenResult = await tokenService.GenerateForTenantAsync(user, resolution.CompanyPublicId, cancellationToken);
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
