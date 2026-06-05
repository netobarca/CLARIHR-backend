using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.IdentityAccess;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.CompanyUsers;

internal sealed class UpdateCompanyUserCommandHandler(
    IUserRepository userRepository,
    IUserCompanyRepository userCompanyRepository,
    IIamAdministrationRepository iamRepository,
    ICompanyUserAuthorizationService authorizationService,
    IRbacAuthorizationService rbacAuthorizationService,
    ITenantContext tenantContext,
    IFieldPermissionService fieldPermissionService,
    IFieldSerializationService fieldSerializationService,
    IUnitOfWork unitOfWork,
    IAuditService auditService,
    ILogger<UpdateCompanyUserCommandHandler> logger)
    : ICommandHandler<UpdateCompanyUserCommand, CompanyUserResponse>
{
    public async Task<Result<CompanyUserResponse>> Handle(
        UpdateCompanyUserCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(RbacPermissionAction.Update, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyUserResponse>.Failure(authorizationResult.Error);
        }

        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.TenantContextRequired);
        }

        var fieldAccessResult = await fieldPermissionService.GetCurrentUserAccessProfileAsync(
            CompanyUserFieldKeys.ResourceKey,
            cancellationToken);
        if (fieldAccessResult.IsFailure)
        {
            return Result<CompanyUserResponse>.Failure(fieldAccessResult.Error);
        }

        var companyPublicId = tenantContext.TenantId.Value;
        var user = await userRepository.GetByPublicIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.UserNotFound);
        }

        var membership = await userCompanyRepository.FindByUserPublicIdAsync(companyPublicId, command.UserId, cancellationToken);
        if (membership is null)
        {
            return await userCompanyRepository.UserExistsOutsideCompanyAsync(companyPublicId, command.UserId, cancellationToken)
                ? Result<CompanyUserResponse>.Failure(AuthorizationErrors.TenantMismatch(CompanyUserFieldKeys.ResourceKey, RbacPermissionAction.Update))
                : Result<CompanyUserResponse>.Failure(CompanyUserErrors.UserNotFound);
        }

        var currentState = await userCompanyRepository.GetUserAsync(companyPublicId, command.UserId, cancellationToken);

        // Weak-ETag concurrency check. The API layer rejects a missing If-Match with 400 before
        // dispatching, so ExpectedETag is non-null on the request path; it is null only for direct
        // (non-HTTP) callers, which skip the check.
        if (command.ExpectedETag is not null &&
            currentState is not null &&
            !CompanyUserETag.Matches(command.ExpectedETag, currentState))
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.ConcurrencyConflict);
        }

        var requestedRoleIds = command.RoleIds
            .Distinct()
            .ToArray();
        var roles = await iamRepository.GetRolesByPublicIdsAsync(requestedRoleIds, cancellationToken);
        if (roles.Count != requestedRoleIds.Length)
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.RoleNotFound);
        }

        var rolesByPublicId = roles.ToDictionary(static role => role.PublicId);
        var desiredRoles = requestedRoleIds
            .Select(roleId => rolesByPublicId[roleId])
            .ToArray();
        var primaryRole = desiredRoles[0];

        if (await userCompanyRepository.IsLastActiveAdministratorAsync(companyPublicId, command.UserId, cancellationToken) &&
            !desiredRoles.Any(CompanyUserManagementHelpers.IsAdministrativeRole))
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.LastActiveAdministratorRequired);
        }

        var changedFields = CompanyUserManagementHelpers.GetChangedUpdateFieldKeys(
            user,
            desiredRoles,
            currentState?.Roles ?? Array.Empty<CompanyUserRoleResponse>(),
            command);
        var fieldAuthorizationResult = await rbacAuthorizationService.AuthorizeFieldsAsync(
            CompanyUserFieldKeys.ResourceKey,
            RbacPermissionAction.Update,
            changedFields,
            cancellationToken);
        if (fieldAuthorizationResult.IsFailure)
        {
            return Result<CompanyUserResponse>.Failure(fieldAuthorizationResult.Error);
        }

        var beforeFirstName = currentState?.FirstName ?? user.FirstName;
        var beforeLastName = currentState?.LastName ?? user.LastName;
        var beforeRoles = currentState?.Roles ?? Array.Empty<CompanyUserRoleResponse>();
        var beforeSnapshot = currentState is null
            ? CompanyUserAuditMapper.CreateSnapshot(user, membership, desiredRoles)
            : CompanyUserAuditMapper.CreateSnapshot(
                user.PublicId,
                currentState.Email ?? user.Email,
                currentState.FirstName ?? user.FirstName,
                currentState.LastName ?? user.LastName,
                currentState.Roles,
                currentState.Status?.ToString() ?? user.Status.ToString(),
                membership.Status.ToString());

        user.UpdateProfile(command.FirstName, command.LastName);
        membership.ChangeRole(primaryRole.Id);

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
                user.Status == UserStatus.Active);
            iamUser.SetTenantId(companyPublicId);
            iamUser.SyncRoles(desiredRoles);
            CompanyUserManagementHelpers.StampTenant(iamUser.RoleAssignments, companyPublicId);
            iamRepository.AddUser(iamUser);
        }
        else
        {
            iamUser.UpdateProfile(user.FirstName, user.LastName);
            iamUser.SetActive(user.Status == UserStatus.Active);
            iamUser.SyncRoles(desiredRoles);
            CompanyUserManagementHelpers.StampTenant(iamUser.RoleAssignments, companyPublicId);
        }

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserUpdated,
                AuditEntityTypes.User,
                user.PublicId,
                EntityKey: user.Email,
                AuditActions.Update,
                $"Updated user {user.Email}.",
                beforeSnapshot,
                CompanyUserAuditMapper.CreateSnapshot(user, membership, desiredRoles),
                CompanyUserAuditMapper.CreateUpdateDiff(
                    beforeFirstName,
                    user.FirstName,
                    beforeLastName,
                    user.LastName,
                    beforeRoles,
                    CompanyUserAuditMapper.MapRoles(desiredRoles))),
            cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "CompanyUserUpdated tenant {TenantId} user {UserPublicId} roleCount {RoleCount}",
            companyPublicId,
            user.PublicId,
            desiredRoles.Length);

        var response = await userCompanyRepository.GetUserAsync(companyPublicId, user.PublicId, cancellationToken);
        return response is null
            ? Result<CompanyUserResponse>.Failure(CompanyUserErrors.UserNotFound)
            : Result<CompanyUserResponse>.Success(
                CompanyUserManagementHelpers.Filter(response, fieldAccessResult.Value, fieldSerializationService)
                    with { WeakETag = CompanyUserETag.Compute(response) });
    }
}
