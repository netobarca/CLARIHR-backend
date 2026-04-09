using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.CompanyUsers;

internal sealed class ResetInvitationCommandHandler(
    IUserRepository userRepository,
    IUserCompanyRepository userCompanyRepository,
    ICompanyRepository companyRepository,
    IInvitationTokenRepository invitationTokenRepository,
    IInvitationTokenHasher invitationTokenHasher,
    IEmailService emailService,
    ICompanyUserAuthorizationService authorizationService,
    ITenantContext tenantContext,
    IFieldPermissionService fieldPermissionService,
    IFieldSerializationService fieldSerializationService,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ILogger<ResetInvitationCommandHandler> logger)
    : ICommandHandler<ResetInvitationCommand, CompanyUserInvitationResponse>
{
    public async Task<Result<CompanyUserInvitationResponse>> Handle(
        ResetInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(RbacPermissionAction.Update, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyUserInvitationResponse>.Failure(authorizationResult.Error);
        }

        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.TenantContextRequired);
        }

        var fieldAccessResult = await fieldPermissionService.GetCurrentUserAccessProfileAsync(
            CompanyUserFieldKeys.ResourceKey,
            cancellationToken);
        if (fieldAccessResult.IsFailure)
        {
            return Result<CompanyUserInvitationResponse>.Failure(fieldAccessResult.Error);
        }

        var companyPublicId = tenantContext.TenantId.Value;
        var company = await companyRepository.FindByPublicIdAsync(companyPublicId, cancellationToken);
        if (company is null)
        {
            return Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.CompanyNotFound);
        }

        var user = await userRepository.GetByPublicIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.UserNotFound);
        }

        var membership = await userCompanyRepository.FindByUserPublicIdAsync(companyPublicId, command.UserId, cancellationToken);
        if (membership is null)
        {
            return await userCompanyRepository.UserExistsOutsideCompanyAsync(companyPublicId, command.UserId, cancellationToken)
                ? Result<CompanyUserInvitationResponse>.Failure(AuthorizationErrors.TenantMismatch(CompanyUserFieldKeys.ResourceKey, RbacPermissionAction.Update))
                : Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.UserNotFound);
        }

        if (user.AuthProvider != AuthProvider.Local)
        {
            return Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.InvitationNotSupportedForExternalUser);
        }

        var currentState = await userCompanyRepository.GetUserAsync(companyPublicId, command.UserId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var issuedAt = dateTimeProvider.UtcNow;
        var invitationExpiresUtc = issuedAt.AddHours(CompanyUserConstants.InvitationExpirationHours);
        var rawToken = CompanyUserManagementHelpers.CreateRawToken();

        await invitationTokenRepository.RevokeActiveTokensAsync(user.Id, company.Id, issuedAt, cancellationToken);
        invitationTokenRepository.Add(InvitationToken.Issue(
            user.Id,
            company.Id,
            invitationTokenHasher.Hash(rawToken),
            invitationExpiresUtc));

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserInvitationReset,
                AuditEntityTypes.User,
                user.PublicId,
                EntityKey: user.Email,
                AuditActions.InvitationReset,
                $"Reset invitation for user {user.Email}.",
                currentState is null
                    ? null
                    : CompanyUserAuditMapper.CreateSnapshot(
                        user.PublicId,
                        currentState.Email ?? user.Email,
                        currentState.FirstName ?? user.FirstName,
                        currentState.LastName ?? user.LastName,
                        currentState.Roles,
                        currentState.Status?.ToString() ?? user.Status.ToString(),
                        membership.Status.ToString()),
                CompanyUserAuditMapper.CreateInvitationSnapshot(
                    user.PublicId,
                    currentState?.Email ?? user.Email,
                    currentState?.FirstName ?? user.FirstName,
                    currentState?.LastName ?? user.LastName,
                    currentState?.Roles ?? Array.Empty<CompanyUserRoleResponse>(),
                    currentState?.Status?.ToString() ?? user.Status.ToString(),
                    membership.Status.ToString(),
                    invitationExpiresUtc)),
            cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailService.SendCompanyUserInvitationAsync(
            new CompanyUserInvitationEmailMessage(
                user.Email,
                user.FirstName,
                user.LastName,
                company.Name,
                rawToken,
                invitationExpiresUtc,
                CompanyUserInvitationEmailKind.ResetInvitation),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "CompanyUserInvitationReset tenant {TenantId} user {UserPublicId}",
            companyPublicId,
            user.PublicId);

        var response = await userCompanyRepository.GetUserAsync(companyPublicId, user.PublicId, cancellationToken);
        return response is null
            ? Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.UserNotFound)
            : Result<CompanyUserInvitationResponse>.Success(new CompanyUserInvitationResponse(
                CompanyUserManagementHelpers.Filter(response, fieldAccessResult.Value, fieldSerializationService),
                invitationExpiresUtc));
    }
}
