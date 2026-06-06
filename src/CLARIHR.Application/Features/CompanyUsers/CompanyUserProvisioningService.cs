using System.Security.Cryptography;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.Features.CompanyUsers;

internal sealed class CompanyUserProvisioningService(
    IUserRepository userRepository,
    IUserCompanyRepository userCompanyRepository,
    ICompanyRepository companyRepository,
    IIamAdministrationRepository iamRepository,
    IInvitationTokenRepository invitationTokenRepository,
    IInvitationTokenHasher invitationTokenHasher,
    IEmailService emailService,
    IPasswordHasher passwordHasher,
    IPersonnelFileRepository personnelFileRepository,
    IUserPreferenceRepository userPreferenceRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService) : ICompanyUserProvisioningService
{
    public async Task<Result<CompanyUserProvisioningResult>> ProvisionAsync(
        CompanyUserProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.FindByPublicIdAsync(request.CompanyPublicId, cancellationToken);
        if (company is null)
        {
            return Result<CompanyUserProvisioningResult>.Failure(CompanyUserErrors.CompanyNotFound);
        }

        var roles = await iamRepository.GetRolesByPublicIdsAsync([request.RoleId], cancellationToken);
        if (roles.Count != 1)
        {
            return Result<CompanyUserProvisioningResult>.Failure(CompanyUserErrors.RoleNotFound);
        }

        var role = roles[0];

        // PV1 (defense-in-depth): the role is resolved through the EF global tenant filter, which
        // is only active when an ambient tenant is set. This service is tenant-parameterized, so it
        // must not trust the ambient filter — validate the resolved role belongs to the requested
        // tenant explicitly. Without this, a future system/background caller (no ambient tenant)
        // could provision a cross-tenant role.
        if (role.TenantId != request.CompanyPublicId)
        {
            return Result<CompanyUserProvisioningResult>.Failure(CompanyUserErrors.RoleNotFound);
        }

        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        var wasCreated = false;
        var membershipReused = false;
        var invitationIssued = false;
        DateTime? invitationExpiresUtc = null;

        UserCompanyMembership? membership = null;
        if (user is not null)
        {
            membership = await userCompanyRepository.GetMembershipAsync(user.Id, request.CompanyPublicId, cancellationToken);
            if (membership is not null)
            {
                if (!request.AllowExistingMembershipReuse)
                {
                    return Result<CompanyUserProvisioningResult>.Failure(CompanyUserErrors.UserAlreadyInCompany);
                }

                membership.ChangeRole(role.Id);
                membership.Reactivate();
                membershipReused = true;
            }
            else
            {
                var hasAnyMembership = await userCompanyRepository.HasAnyMembershipAsync(user.Id, cancellationToken);
                if (hasAnyMembership)
                {
                    var primaryCompanyPublicId = await userCompanyRepository.GetPrimaryCompanyPublicIdAsync(user.Id, cancellationToken);
                    if (!primaryCompanyPublicId.HasValue || primaryCompanyPublicId.Value != request.CompanyPublicId)
                    {
                        return Result<CompanyUserProvisioningResult>.Failure(CompanyUserErrors.UserAssignedToAnotherCompany);
                    }
                }

                membership = UserCompanyMembership.Create(
                    user.Id,
                    company.Id,
                    role.Id,
                    isPrimary: !hasAnyMembership);
                userCompanyRepository.Add(membership);
            }
        }
        else
        {
            var temporaryPassword = CreateTemporaryPassword(request.FirstName, request.LastName, request.Email);
            user = User.InviteLocalWithTemporaryPassword(
                request.FirstName,
                request.LastName,
                request.Email,
                passwordHasher.Hash(temporaryPassword),
                request.Country,
                request.Source ?? CompanyUserConstants.InvitationSource);

            await userRepository.AddAsync(user, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            userPreferenceRepository.Add(UserPreference.Create(user.Id));
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            membership = UserCompanyMembership.Create(
                user.Id,
                company.Id,
                role.Id,
                isPrimary: true);
            userCompanyRepository.Add(membership);

            wasCreated = true;
            invitationIssued = true;
        }

        if (user.AuthProvider == AuthProvider.Local &&
            user.Status != UserStatus.Active &&
            !wasCreated)
        {
            var temporaryPassword = CreateTemporaryPassword(user.FirstName, user.LastName, user.Email);
            user.SetPendingActivationPassword(passwordHasher.Hash(temporaryPassword));
            invitationIssued = true;
        }

        var iamUser = await iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
            request.CompanyPublicId,
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
            iamUser.SetTenantId(request.CompanyPublicId);
            iamUser.SyncRoles([role]);
            CompanyUserManagementHelpers.StampTenant(iamUser.RoleAssignments, request.CompanyPublicId);
            iamRepository.AddUser(iamUser);
        }
        else
        {
            iamUser.UpdateProfile(user.FirstName, user.LastName);
            iamUser.SetActive(user.Status == UserStatus.Active);
            iamUser.SyncRoles([role]);
            CompanyUserManagementHelpers.StampTenant(iamUser.RoleAssignments, request.CompanyPublicId);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (invitationIssued)
        {
            var issuedAt = dateTimeProvider.UtcNow;
            invitationExpiresUtc = issuedAt.AddHours(CompanyUserConstants.InvitationExpirationHours);
            var rawToken = CompanyUserManagementHelpers.CreateRawToken();

            await invitationTokenRepository.RevokeActiveTokensAsync(user.Id, company.Id, issuedAt, cancellationToken);
            invitationTokenRepository.Add(InvitationToken.Issue(
                user.Id,
                company.Id,
                invitationTokenHasher.Hash(rawToken),
                invitationExpiresUtc.Value));

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await emailService.SendCompanyUserInvitationAsync(
                new CompanyUserInvitationEmailMessage(
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    company.Name,
                    rawToken,
                    invitationExpiresUtc.Value,
                    CompanyUserInvitationEmailKind.Invitation),
                cancellationToken);
        }

        // PV2: provisioning assigns a role (and may invite) without going through the CompanyUsers
        // controller, so it must emit its own per-user audit event — otherwise the privilege change
        // is only traceable at the file/slot level. Use LogForTenantAsync (explicit tenant) to stay
        // consistent with PV1 and not depend on the ambient tenant context.
        if (invitationIssued)
        {
            await auditService.LogForTenantAsync(
                request.CompanyPublicId,
                new AuditLogEntry(
                    AuditEventTypes.UserInvited,
                    AuditEntityTypes.User,
                    user.PublicId,
                    EntityKey: user.Email,
                    AuditActions.Invite,
                    $"Provisioned and invited user {user.Email}.",
                    After: CompanyUserAuditMapper.CreateInvitationSnapshot(user, membership!, [role], invitationExpiresUtc!.Value)),
                cancellationToken);
        }
        else
        {
            await auditService.LogForTenantAsync(
                request.CompanyPublicId,
                new AuditLogEntry(
                    AuditEventTypes.UserUpdated,
                    AuditEntityTypes.User,
                    user.PublicId,
                    EntityKey: user.Email,
                    AuditActions.Update,
                    $"Provisioned user {user.Email} (role assignment updated).",
                    After: CompanyUserAuditMapper.CreateSnapshot(user, membership!, [role])),
                cancellationToken);
        }

        var response = await userCompanyRepository.GetUserAsync(request.CompanyPublicId, user.PublicId, cancellationToken);
        if (response is null)
        {
            return Result<CompanyUserProvisioningResult>.Failure(CompanyUserErrors.UserNotFound);
        }

        return Result<CompanyUserProvisioningResult>.Success(
            new CompanyUserProvisioningResult(
                user,
                response,
                invitationExpiresUtc,
                wasCreated,
                membershipReused,
                invitationIssued));
    }

    public async Task<Result<int>> SyncRoleAssignmentsForPositionSlotAsync(
        Guid companyPublicId,
        Guid assignedPositionSlotId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var roles = await iamRepository.GetRolesByPublicIdsAsync([roleId], cancellationToken);
        if (roles.Count != 1)
        {
            return Result<int>.Failure(CompanyUserErrors.RoleNotFound);
        }

        var role = roles[0];

        // PV1 (defense-in-depth): validate the role belongs to the requested tenant explicitly
        // rather than trusting the ambient EF tenant filter (see ProvisionAsync for the rationale).
        if (role.TenantId != companyPublicId)
        {
            return Result<int>.Failure(CompanyUserErrors.RoleNotFound);
        }

        var linkedUserIds = await personnelFileRepository.GetLinkedUserIdsByAssignedPositionSlotAsync(
            companyPublicId,
            assignedPositionSlotId,
            cancellationToken);
        if (linkedUserIds.Count == 0)
        {
            return Result<int>.Success(0);
        }

        // PV3: batch-resolve the three aggregates once for the whole linked-user set instead of
        // issuing 3 queries per user (N+1). The EF repositories override the batch methods with a
        // single query each; the in-memory loop below mutates and audits without further round-trips.
        var users = await userRepository.GetByPublicIdsAsync(linkedUserIds, cancellationToken);
        if (users.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var membershipsByUserId = (await userCompanyRepository.GetMembershipsAsync(
                users.Select(user => user.Id).ToArray(),
                companyPublicId,
                cancellationToken))
            .ToDictionary(membership => membership.UserId);

        var iamUsersByLinkedPublicId = (await iamRepository.GetUsersByTenantAndLinkedUserPublicIdsAsync(
                companyPublicId,
                users.Select(user => user.PublicId).ToArray(),
                includeRoles: true,
                cancellationToken))
            .Where(iamUser => iamUser.LinkedUserPublicId.HasValue)
            .ToDictionary(iamUser => iamUser.LinkedUserPublicId!.Value);

        var updatedCount = 0;
        foreach (var user in users)
        {
            if (membershipsByUserId.TryGetValue(user.Id, out var membership))
            {
                membership.ChangeRole(role.Id);
            }

            if (iamUsersByLinkedPublicId.TryGetValue(user.PublicId, out var iamUser))
            {
                iamUser.SyncRoles([role]);
                CompanyUserManagementHelpers.StampTenant(iamUser.RoleAssignments, companyPublicId);
            }

            // PV2: the position-slot role cascade changes the privilege of each linked user, so emit
            // a per-user audit event — the slot-level PositionSlotUpdated entry records that the slot
            // role changed but not WHICH users were re-assigned.
            await auditService.LogForTenantAsync(
                companyPublicId,
                new AuditLogEntry(
                    AuditEventTypes.UserUpdated,
                    AuditEntityTypes.User,
                    user.PublicId,
                    EntityKey: user.Email,
                    AuditActions.Update,
                    $"Synced role for user {user.Email} from position slot {assignedPositionSlotId}.",
                    After: CompanyUserAuditMapper.CreateSnapshot(
                        user.PublicId,
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        CompanyUserAuditMapper.MapRoles([role]),
                        user.Status.ToString(),
                        membership?.Status.ToString() ?? string.Empty)),
                cancellationToken);

            updatedCount++;
        }

        return Result<int>.Success(updatedCount);
    }

    private string CreateTemporaryPassword(string firstName, string lastName, string email)
    {
        while (true)
        {
            var candidate = BuildPasswordCandidate();
            if (!AuthValidationRules.GetPasswordPolicyViolations(candidate, firstName, lastName, email).Any())
            {
                return candidate;
            }
        }
    }

    private static string BuildPasswordCandidate()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%*_-+=";
        var all = upper + lower + digits + special;

        Span<char> password = stackalloc char[16];
        password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        password[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

        for (var index = 4; index < password.Length; index++)
        {
            password[index] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        for (var index = password.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (password[index], password[swapIndex]) = (password[swapIndex], password[index]);
        }

        return new string(password);
    }
}
