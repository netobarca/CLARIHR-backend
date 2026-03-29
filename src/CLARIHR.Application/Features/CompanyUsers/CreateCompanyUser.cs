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
using CLARIHR.Domain.IdentityAccess;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.CompanyUsers;

internal sealed class CreateCompanyUserCommandHandler(
    IUserRepository userRepository,
    IUserCompanyRepository userCompanyRepository,
    ICompanyRepository companyRepository,
    IIamAdministrationRepository iamRepository,
    IInvitationTokenRepository invitationTokenRepository,
    IInvitationTokenHasher invitationTokenHasher,
    IEmailService emailService,
    ICompanyUserAuthorizationService authorizationService,
    IRbacAuthorizationService rbacAuthorizationService,
    ITenantContext tenantContext,
    IFieldPermissionService fieldPermissionService,
    IFieldSerializationService fieldSerializationService,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ILogger<CreateCompanyUserCommandHandler> logger)
    : ICommandHandler<CreateCompanyUserCommand, CompanyUserInvitationResponse>
{
    public async Task<Result<CompanyUserInvitationResponse>> Handle(
        CreateCompanyUserCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(RbacPermissionAction.Create, cancellationToken);
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

        var fieldAuthorizationResult = await rbacAuthorizationService.AuthorizeFieldsAsync(
            CompanyUserFieldKeys.ResourceKey,
            RbacPermissionAction.Create,
            CompanyUserManagementHelpers.GetCreateFieldKeys(),
            cancellationToken);
        if (fieldAuthorizationResult.IsFailure)
        {
            return Result<CompanyUserInvitationResponse>.Failure(fieldAuthorizationResult.Error);
        }

        var companyPublicId = tenantContext.TenantId.Value;
        var company = await companyRepository.FindByPublicIdAsync(companyPublicId, cancellationToken);
        if (company is null)
        {
            return Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.CompanyNotFound);
        }

        var role = await iamRepository.FindRoleByPublicIdAsync(command.RoleId, includePermissions: true, cancellationToken);
        if (role is null)
        {
            return Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.RoleNotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (user is not null)
        {
            if (await userCompanyRepository.ExistsInCompanyAsync(companyPublicId, user.NormalizedEmail, cancellationToken))
            {
                return Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.UserAlreadyInCompany);
            }

            if (await userCompanyRepository.HasAnyMembershipAsync(user.Id, cancellationToken))
            {
                var existingCompanyPublicId = await userCompanyRepository.GetPrimaryCompanyPublicIdAsync(user.Id, cancellationToken);
                if (!existingCompanyPublicId.HasValue || existingCompanyPublicId.Value != companyPublicId)
                {
                    return Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.UserAssignedToAnotherCompany);
                }
            }
        }
        else
        {
            user = User.InviteLocal(
                command.FirstName,
                command.LastName,
                command.Email,
                country: null,
                source: CompanyUserConstants.InvitationSource);

            await userRepository.AddAsync(user, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var membership = UserCompanyMembership.Create(
            user.Id,
            company.Id,
            role.Id,
            isPrimary: !await userCompanyRepository.HasAnyMembershipAsync(user.Id, cancellationToken));
        userCompanyRepository.Add(membership);

        var iamUser = await iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
            companyPublicId,
            user.PublicId,
            includeRoles: true,
            cancellationToken);
        if (iamUser is null)
        {
            iamUser = IamUser.CreateLinked(
                user.PublicId,
                user.FirstName,
                user.LastName,
                user.Email,
                isActive: user.Status == UserStatus.Active);
            iamUser.SetTenantId(companyPublicId);
            iamUser.SyncRoles([role]);
            CompanyUserManagementHelpers.StampTenant(iamUser.RoleAssignments, companyPublicId);
            iamRepository.AddUser(iamUser);
        }
        else
        {
            iamUser.UpdateProfile(user.FirstName, user.LastName);
            iamUser.SetActive(user.Status == UserStatus.Active);
            iamUser.SyncRoles([role]);
            CompanyUserManagementHelpers.StampTenant(iamUser.RoleAssignments, companyPublicId);
        }

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
                AuditEventTypes.UserInvited,
                AuditEntityTypes.User,
                user.PublicId,
                EntityKey: user.Email,
                AuditActions.Invite,
                $"Invited user {user.Email}.",
                After: CompanyUserAuditMapper.CreateInvitationSnapshot(user, membership, role, invitationExpiresUtc)),
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
                CompanyUserInvitationEmailKind.Invitation),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "CompanyUserInvited tenant {TenantId} user {UserPublicId} role {RolePublicId}",
            companyPublicId,
            user.PublicId,
            role.PublicId);

        var response = await userCompanyRepository.GetUserAsync(companyPublicId, user.PublicId, cancellationToken);
        return response is null
            ? Result<CompanyUserInvitationResponse>.Failure(CompanyUserErrors.UserNotFound)
            : Result<CompanyUserInvitationResponse>.Success(new CompanyUserInvitationResponse(
                CompanyUserManagementHelpers.Filter(response, fieldAccessResult.Value, fieldSerializationService),
                invitationExpiresUtc));
    }
}
