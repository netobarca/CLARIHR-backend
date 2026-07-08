using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.EmployeeRelations;

/// <summary>
/// Shared handler gate for the employee-relations configuration masters (recognition types,
/// disciplinary-action types, disciplinary-action causes — REQ-003). Mirrors
/// <c>ILeaveConfigurationAuthorizationService</c>.
/// </summary>
public interface IEmployeeRelationsConfigurationAuthorizationService
{
    Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken);

    Error TenantMismatch(RbacPermissionAction action);
}
