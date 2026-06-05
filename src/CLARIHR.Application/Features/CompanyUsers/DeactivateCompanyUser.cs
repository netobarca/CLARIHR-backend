using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
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
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.CompanyUsers;

internal sealed class DeactivateCompanyUserCommandHandler(
    IUserRepository userRepository,
    IUserCompanyRepository userCompanyRepository,
    IIamAdministrationRepository iamRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ICompanyUserAuthorizationService authorizationService,
    ITenantContext tenantContext,
    IFieldPermissionService fieldPermissionService,
    IFieldSerializationService fieldSerializationService,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ILogger<DeactivateCompanyUserCommandHandler> logger)
    : ICommandHandler<DeactivateCompanyUserCommand, CompanyUserResponse>
{
    public async Task<Result<CompanyUserResponse>> Handle(
        DeactivateCompanyUserCommand command,
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

        if (await userCompanyRepository.IsLastActiveAdministratorAsync(companyPublicId, command.UserId, cancellationToken))
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.LastActiveAdministratorRequired);
        }

        var currentState = await userCompanyRepository.GetUserAsync(companyPublicId, command.UserId, cancellationToken);

        // Weak-ETag concurrency check (the API layer enforces a present If-Match with 400; null skips).
        if (command.ExpectedETag is not null &&
            currentState is not null &&
            !CompanyUserETag.Matches(command.ExpectedETag, currentState))
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.ConcurrencyConflict);
        }

        var beforeStatus = currentState?.Status?.ToString() ?? user.Status.ToString();
        var beforeMembershipStatus = membership.Status.ToString();

        user.Deactivate();
        membership.Deactivate();

        var iamUser = await iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
            companyPublicId,
            user.PublicId,
            includeRoles: false,
            cancellationToken);
        iamUser?.SetActive(false);

        await refreshTokenRepository.RevokeUserTokensAsync(
            user.Id,
            AuthClientType.Core,
            dateTimeProvider.UtcNow,
            "company-user-deactivated",
            cancellationToken);

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserDeactivated,
                AuditEntityTypes.User,
                user.PublicId,
                EntityKey: user.Email,
                AuditActions.Deactivate,
                $"Deactivated user {user.Email}.",
                currentState is null
                    ? null
                    : CompanyUserAuditMapper.CreateSnapshot(
                        user.PublicId,
                        currentState.Email ?? user.Email,
                        currentState.FirstName ?? user.FirstName,
                        currentState.LastName ?? user.LastName,
                        currentState.Roles,
                        beforeStatus,
                        beforeMembershipStatus),
                CompanyUserAuditMapper.CreateSnapshot(
                    user.PublicId,
                    currentState?.Email ?? user.Email,
                    currentState?.FirstName ?? user.FirstName,
                    currentState?.LastName ?? user.LastName,
                    currentState?.Roles ?? Array.Empty<CompanyUserRoleResponse>(),
                    user.Status.ToString(),
                    membership.Status.ToString()),
                CompanyUserAuditMapper.CreateStatusDiff(
                    beforeStatus,
                    user.Status.ToString(),
                    beforeMembershipStatus,
                    membership.Status.ToString())),
            cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "CompanyUserDeactivated tenant {TenantId} user {UserPublicId}",
            companyPublicId,
            user.PublicId);

        var response = await userCompanyRepository.GetUserAsync(companyPublicId, user.PublicId, cancellationToken);
        return response is null
            ? Result<CompanyUserResponse>.Failure(CompanyUserErrors.UserNotFound)
            : Result<CompanyUserResponse>.Success(
                CompanyUserManagementHelpers.Filter(response, fieldAccessResult.Value, fieldSerializationService)
                    with { WeakETag = CompanyUserETag.Compute(response) });
    }
}
