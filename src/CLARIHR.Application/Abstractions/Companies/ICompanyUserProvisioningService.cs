using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Companies;

public sealed record CompanyUserProvisioningRequest(
    Guid CompanyPublicId,
    string Email,
    string FirstName,
    string LastName,
    Guid RoleId,
    string? Country,
    string? Source,
    bool AllowExistingMembershipReuse);

public sealed record CompanyUserProvisioningResult(
    User User,
    CompanyUserResponse UserResponse,
    DateTime? InvitationExpiresUtc,
    bool WasCreated,
    bool MembershipReused,
    bool InvitationIssued);

public interface ICompanyUserProvisioningService
{
    Task<Result<CompanyUserProvisioningResult>> ProvisionAsync(
        CompanyUserProvisioningRequest request,
        CancellationToken cancellationToken);

    Task<Result<int>> SyncRoleAssignmentsForPositionSlotAsync(
        Guid companyPublicId,
        Guid assignedPositionSlotId,
        Guid roleId,
        CancellationToken cancellationToken);
}
