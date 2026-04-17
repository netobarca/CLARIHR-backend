using System.Security.Cryptography;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
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
    IDateTimeProvider dateTimeProvider) : ICompanyUserProvisioningService
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
        var linkedUserIds = await personnelFileRepository.GetLinkedUserIdsByAssignedPositionSlotAsync(
            companyPublicId,
            assignedPositionSlotId,
            cancellationToken);

        var updatedCount = 0;
        foreach (var linkedUserId in linkedUserIds)
        {
            var user = await userRepository.GetByPublicIdAsync(linkedUserId, cancellationToken);
            if (user is null)
            {
                continue;
            }

            var membership = await userCompanyRepository.GetMembershipAsync(user.Id, companyPublicId, cancellationToken);
            if (membership is not null)
            {
                membership.ChangeRole(role.Id);
            }

            var iamUser = await iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
                companyPublicId,
                linkedUserId,
                includeRoles: true,
                cancellationToken);
            if (iamUser is not null)
            {
                iamUser.SyncRoles([role]);
                CompanyUserManagementHelpers.StampTenant(iamUser.RoleAssignments, companyPublicId);
            }

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
