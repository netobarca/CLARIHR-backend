using System.Security.Cryptography;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.CompanyUsers;

public sealed record CompanyUserSummaryResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    Guid? RoleId,
    string? Role,
    UserStatus? Status);

public sealed record CompanyUserResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    Guid? RoleId,
    string? Role,
    UserStatus? Status);

public sealed record CompanyUserInvitationResponse(
    CompanyUserResponse User,
    DateTime InvitationExpiresUtc);

public sealed record GetCompanyUsersQuery(
    int Page = 1,
    int PageSize = 20,
    UserStatus? Status = null,
    Guid? RoleId = null,
    string? Search = null) : IQuery<PagedResponse<CompanyUserSummaryResponse>>;

public sealed record CreateCompanyUserCommand(
    string Email,
    string FirstName,
    string LastName,
    Guid RoleId) : ICommand<CompanyUserInvitationResponse>;

public sealed record UpdateCompanyUserCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    Guid RoleId) : ICommand<CompanyUserResponse>;

public sealed record DeactivateCompanyUserCommand(Guid UserId) : ICommand<CompanyUserResponse>;

public sealed record ReactivateCompanyUserCommand(Guid UserId) : ICommand<CompanyUserResponse>;

public sealed record ResetInvitationCommand(Guid UserId) : ICommand<CompanyUserInvitationResponse>;

internal sealed class GetCompanyUsersQueryValidator : AbstractValidator<GetCompanyUsersQuery>
{
    public GetCompanyUsersQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.RoleId)
            .NotEqual(Guid.Empty)
            .When(static query => query.RoleId.HasValue);

        RuleFor(query => query.Search)
            .MaximumLength(100)
            .When(static query => !string.IsNullOrWhiteSpace(query.Search));
    }
}

internal sealed class CreateCompanyUserCommandValidator : AbstractValidator<CreateCompanyUserCommand>
{
    public CreateCompanyUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

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

        RuleFor(command => command.RoleId)
            .NotEmpty();
    }
}

internal sealed class UpdateCompanyUserCommandValidator : AbstractValidator<UpdateCompanyUserCommand>
{
    public UpdateCompanyUserCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();

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

        RuleFor(command => command.RoleId)
            .NotEmpty();
    }
}

internal sealed class DeactivateCompanyUserCommandValidator : AbstractValidator<DeactivateCompanyUserCommand>
{
    public DeactivateCompanyUserCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}

internal sealed class ReactivateCompanyUserCommandValidator : AbstractValidator<ReactivateCompanyUserCommand>
{
    public ReactivateCompanyUserCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}

internal sealed class ResetInvitationCommandValidator : AbstractValidator<ResetInvitationCommand>
{
    public ResetInvitationCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}

internal sealed class GetCompanyUsersQueryHandler(
    IUserCompanyRepository userCompanyRepository,
    ICompanyUserAuthorizationService authorizationService,
    ITenantContext tenantContext,
    IFieldPermissionService fieldPermissionService,
    IFieldSerializationService fieldSerializationService)
    : IQueryHandler<GetCompanyUsersQuery, PagedResponse<CompanyUserSummaryResponse>>
{
    public async Task<Result<PagedResponse<CompanyUserSummaryResponse>>> Handle(
        GetCompanyUsersQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(RbacPermissionAction.Read, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CompanyUserSummaryResponse>>.Failure(authorizationResult.Error);
        }

        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PagedResponse<CompanyUserSummaryResponse>>.Failure(CompanyUserErrors.TenantContextRequired);
        }

        var users = await userCompanyRepository.GetUsersAsync(
            tenantContext.TenantId.Value,
            query.Page,
            query.PageSize,
            query.Status,
            query.RoleId,
            query.Search,
            cancellationToken);

        var fieldAccessResult = await fieldPermissionService.GetCurrentUserAccessProfileAsync(
            CompanyUserFieldKeys.ResourceKey,
            cancellationToken);
        if (fieldAccessResult.IsFailure)
        {
            return Result<PagedResponse<CompanyUserSummaryResponse>>.Failure(fieldAccessResult.Error);
        }

        return Result<PagedResponse<CompanyUserSummaryResponse>>.Success(
            CompanyUserManagementHelpers.Filter(users, fieldAccessResult.Value, fieldSerializationService));
    }
}

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

        var iamUser = await iamRepository.FindUserByPublicIdAsync(user.PublicId, includeRoles: true, cancellationToken);
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
        var role = await iamRepository.FindRoleByPublicIdAsync(command.RoleId, includePermissions: true, cancellationToken);
        if (role is null)
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.RoleNotFound);
        }

        if (await userCompanyRepository.IsLastActiveAdministratorAsync(companyPublicId, command.UserId, cancellationToken) &&
            !CompanyUserManagementHelpers.IsAdministrativeRole(role))
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.LastActiveAdministratorRequired);
        }

        var changedFields = CompanyUserManagementHelpers.GetChangedUpdateFieldKeys(user, membership, role, command);
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
        var beforeRoleId = currentState?.RoleId ?? role.PublicId;
        var beforeRoleName = currentState?.Role ?? role.Name;
        var beforeSnapshot = currentState is null
            ? CompanyUserAuditMapper.CreateSnapshot(user, membership, role)
            : CompanyUserAuditMapper.CreateSnapshot(
                user.PublicId,
                currentState.Email ?? user.Email,
                currentState.FirstName ?? user.FirstName,
                currentState.LastName ?? user.LastName,
                currentState.RoleId ?? role.PublicId,
                currentState.Role ?? role.Name,
                currentState.Status?.ToString() ?? user.Status.ToString(),
                membership.Status.ToString());

        user.UpdateProfile(command.FirstName, command.LastName);
        membership.ChangeRole(role.Id);

        var iamUser = await iamRepository.FindUserByPublicIdAsync(command.UserId, includeRoles: true, cancellationToken);
        if (iamUser is null)
        {
            iamUser = IamUser.CreateLinked(
                user.PublicId,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Status == UserStatus.Active);
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

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserUpdated,
                AuditEntityTypes.User,
                user.PublicId,
                EntityKey: user.Email,
                AuditActions.Update,
                $"Updated user {user.Email}.",
                beforeSnapshot,
                CompanyUserAuditMapper.CreateSnapshot(user, membership, role),
                CompanyUserAuditMapper.CreateUpdateDiff(
                    beforeFirstName,
                    user.FirstName,
                    beforeLastName,
                    user.LastName,
                    beforeRoleId,
                    role.PublicId,
                    beforeRoleName,
                    role.Name)),
            cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "CompanyUserUpdated tenant {TenantId} user {UserPublicId} role {RolePublicId}",
            companyPublicId,
            user.PublicId,
            role.PublicId);

        var response = await userCompanyRepository.GetUserAsync(companyPublicId, user.PublicId, cancellationToken);
        return response is null
            ? Result<CompanyUserResponse>.Failure(CompanyUserErrors.UserNotFound)
            : Result<CompanyUserResponse>.Success(
                CompanyUserManagementHelpers.Filter(response, fieldAccessResult.Value, fieldSerializationService));
    }
}

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
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(RbacPermissionAction.Delete, cancellationToken);
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
                ? Result<CompanyUserResponse>.Failure(AuthorizationErrors.TenantMismatch(CompanyUserFieldKeys.ResourceKey, RbacPermissionAction.Delete))
                : Result<CompanyUserResponse>.Failure(CompanyUserErrors.UserNotFound);
        }

        if (await userCompanyRepository.IsLastActiveAdministratorAsync(companyPublicId, command.UserId, cancellationToken))
        {
            return Result<CompanyUserResponse>.Failure(CompanyUserErrors.LastActiveAdministratorRequired);
        }

        var currentState = await userCompanyRepository.GetUserAsync(companyPublicId, command.UserId, cancellationToken);
        var beforeStatus = currentState?.Status?.ToString() ?? user.Status.ToString();
        var beforeMembershipStatus = membership.Status.ToString();

        user.Deactivate();
        membership.Deactivate();

        var iamUser = await iamRepository.FindUserByPublicIdAsync(command.UserId, includeRoles: false, cancellationToken);
        iamUser?.SetActive(false);

        await refreshTokenRepository.RevokeUserTokensAsync(
            user.Id,
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
                        currentState.RoleId ?? Guid.Empty,
                        currentState.Role ?? string.Empty,
                        beforeStatus,
                        beforeMembershipStatus),
                CompanyUserAuditMapper.CreateSnapshot(
                    user.PublicId,
                    currentState?.Email ?? user.Email,
                    currentState?.FirstName ?? user.FirstName,
                    currentState?.LastName ?? user.LastName,
                    currentState?.RoleId ?? Guid.Empty,
                    currentState?.Role ?? string.Empty,
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
                CompanyUserManagementHelpers.Filter(response, fieldAccessResult.Value, fieldSerializationService));
    }
}

internal sealed class ReactivateCompanyUserCommandHandler(
    IUserRepository userRepository,
    IUserCompanyRepository userCompanyRepository,
    IIamAdministrationRepository iamRepository,
    ICompanyUserAuthorizationService authorizationService,
    ITenantContext tenantContext,
    IFieldPermissionService fieldPermissionService,
    IFieldSerializationService fieldSerializationService,
    IUnitOfWork unitOfWork,
    IAuditService auditService,
    ILogger<ReactivateCompanyUserCommandHandler> logger)
    : ICommandHandler<ReactivateCompanyUserCommand, CompanyUserResponse>
{
    public async Task<Result<CompanyUserResponse>> Handle(
        ReactivateCompanyUserCommand command,
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
        var beforeStatus = currentState?.Status?.ToString() ?? user.Status.ToString();
        var beforeMembershipStatus = membership.Status.ToString();

        user.Reactivate();
        membership.Reactivate();

        var iamUser = await iamRepository.FindUserByPublicIdAsync(command.UserId, includeRoles: false, cancellationToken);
        iamUser?.SetActive(user.Status == UserStatus.Active);

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserReactivated,
                AuditEntityTypes.User,
                user.PublicId,
                EntityKey: user.Email,
                AuditActions.Reactivate,
                $"Reactivated user {user.Email}.",
                currentState is null
                    ? null
                    : CompanyUserAuditMapper.CreateSnapshot(
                        user.PublicId,
                        currentState.Email ?? user.Email,
                        currentState.FirstName ?? user.FirstName,
                        currentState.LastName ?? user.LastName,
                        currentState.RoleId ?? Guid.Empty,
                        currentState.Role ?? string.Empty,
                        beforeStatus,
                        beforeMembershipStatus),
                CompanyUserAuditMapper.CreateSnapshot(
                    user.PublicId,
                    currentState?.Email ?? user.Email,
                    currentState?.FirstName ?? user.FirstName,
                    currentState?.LastName ?? user.LastName,
                    currentState?.RoleId ?? Guid.Empty,
                    currentState?.Role ?? string.Empty,
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
            "CompanyUserReactivated tenant {TenantId} user {UserPublicId} resultingStatus {Status}",
            companyPublicId,
            user.PublicId,
            user.Status);

        var response = await userCompanyRepository.GetUserAsync(companyPublicId, user.PublicId, cancellationToken);
        return response is null
            ? Result<CompanyUserResponse>.Failure(CompanyUserErrors.UserNotFound)
            : Result<CompanyUserResponse>.Success(
                CompanyUserManagementHelpers.Filter(response, fieldAccessResult.Value, fieldSerializationService));
    }
}

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
                        currentState.RoleId ?? Guid.Empty,
                        currentState.Role ?? string.Empty,
                        currentState.Status?.ToString() ?? user.Status.ToString(),
                        membership.Status.ToString()),
                CompanyUserAuditMapper.CreateInvitationSnapshot(
                    user.PublicId,
                    currentState?.Email ?? user.Email,
                    currentState?.FirstName ?? user.FirstName,
                    currentState?.LastName ?? user.LastName,
                    currentState?.RoleId ?? Guid.Empty,
                    currentState?.Role ?? string.Empty,
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

internal static class CompanyUserManagementHelpers
{
    public static bool IsAdministrativeRole(IamRole role)
    {
        var normalizedManageUsers = CompanyUserPermissionCodes.ManageUsers.ToUpperInvariant();
        var normalizedManageAdministration = IdentityPermissionCodes.ManageAdministration.ToUpperInvariant();

        return role.PermissionAssignments.Any(assignment =>
            assignment.Permission.NormalizedCode == normalizedManageUsers ||
            assignment.Permission.NormalizedCode == normalizedManageAdministration);
    }

    public static void StampTenant(IEnumerable<IamUserRoleAssignment> assignments, Guid tenantId)
    {
        foreach (var assignment in assignments)
        {
            assignment.SetTenantId(tenantId);
        }
    }

    public static IReadOnlyCollection<string> GetCreateFieldKeys() =>
    [
        CompanyUserFieldKeys.Email,
        CompanyUserFieldKeys.FirstName,
        CompanyUserFieldKeys.LastName,
        CompanyUserFieldKeys.Role
    ];

    public static IReadOnlyCollection<string> GetChangedUpdateFieldKeys(
        User user,
        UserCompanyMembership membership,
        IamRole desiredRole,
        UpdateCompanyUserCommand command)
    {
        var changedFields = new List<string>(3);

        if (!string.Equals(user.FirstName, command.FirstName, StringComparison.Ordinal))
        {
            changedFields.Add(CompanyUserFieldKeys.FirstName);
        }

        if (!string.Equals(user.LastName, command.LastName, StringComparison.Ordinal))
        {
            changedFields.Add(CompanyUserFieldKeys.LastName);
        }

        if (membership.RoleId != desiredRole.Id)
        {
            changedFields.Add(CompanyUserFieldKeys.Role);
        }

        return changedFields;
    }

    public static PagedResponse<CompanyUserSummaryResponse> Filter(
        PagedResponse<CompanyUserSummaryResponse> response,
        FieldAccessProfile fieldAccessProfile,
        IFieldSerializationService fieldSerializationService)
    {
        var items = response.Items
            .Select(item => Filter(item, fieldAccessProfile, fieldSerializationService))
            .ToArray();

        return new PagedResponse<CompanyUserSummaryResponse>(items, response.PageNumber, response.PageSize, response.TotalCount);
    }

    public static CompanyUserSummaryResponse Filter(
        CompanyUserSummaryResponse response,
        FieldAccessProfile fieldAccessProfile,
        IFieldSerializationService fieldSerializationService) =>
        new(
            response.Id,
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Email), response.Email),
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.FirstName), response.FirstName),
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.LastName), response.LastName),
            response.RoleId.HasValue
                ? fieldSerializationService.SerializeGuid(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Role), response.RoleId.Value)
                : null,
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Role), response.Role),
            response.Status.HasValue
                ? fieldSerializationService.SerializeEnum(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Status), response.Status.Value)
                : null);

    public static CompanyUserResponse Filter(
        CompanyUserResponse response,
        FieldAccessProfile fieldAccessProfile,
        IFieldSerializationService fieldSerializationService) =>
        new(
            response.Id,
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Email), response.Email),
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.FirstName), response.FirstName),
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.LastName), response.LastName),
            response.RoleId.HasValue
                ? fieldSerializationService.SerializeGuid(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Role), response.RoleId.Value)
                : null,
            fieldSerializationService.SerializeString(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Role), response.Role),
            response.Status.HasValue
                ? fieldSerializationService.SerializeEnum(fieldAccessProfile.GetRule(CompanyUserFieldKeys.Status), response.Status.Value)
                : null);

    public static string CreateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
