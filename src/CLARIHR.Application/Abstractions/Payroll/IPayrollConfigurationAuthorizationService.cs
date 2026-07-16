using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.Payroll;

/// <summary>
/// Shared handler gate for the payroll configuration masters (payroll definitions — REQ-012 PR-1; work
/// schedules join in PR-3). Read requires <c>PayrollConfiguration.Read</c> / <c>.Manage</c> / IAM
/// super-admin; Manage requires <c>PayrollConfiguration.Manage</c> / IAM super-admin. Mirrors
/// <c>ILeaveConfigurationAuthorizationService</c>.
/// </summary>
public interface IPayrollConfigurationAuthorizationService
{
    Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken);

    Error TenantMismatch(RbacPermissionAction action);
}
