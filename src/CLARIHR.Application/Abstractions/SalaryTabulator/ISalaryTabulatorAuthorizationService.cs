using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.SalaryTabulator;

public interface ISalaryTabulatorAuthorizationService
{
    Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanRequestAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanApproveAsync(Guid companyId, CancellationToken cancellationToken);

    Error TenantMismatch(RbacPermissionAction action);
}
