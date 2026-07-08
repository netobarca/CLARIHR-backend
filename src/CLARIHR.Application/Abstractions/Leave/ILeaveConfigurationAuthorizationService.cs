using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.Leave;

/// <summary>
/// Shared handler gate for the leave-configuration masters (medical clinics, incapacity
/// risks/types, company holidays, payroll periods). Mirrors <c>ICostCenterAuthorizationService</c>.
/// </summary>
public interface ILeaveConfigurationAuthorizationService
{
    Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken);

    Error TenantMismatch(RbacPermissionAction action);
}
