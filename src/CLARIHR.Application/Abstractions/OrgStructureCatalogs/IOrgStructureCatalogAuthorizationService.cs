using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.OrgStructureCatalogs;

public interface IOrgStructureCatalogAuthorizationService
{
    Task<Result<Guid>> EnsureAccountScopeAsync(CancellationToken cancellationToken);

    Task<Result> EnsureCanReadTenantAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanManageTenantAsync(Guid companyId, CancellationToken cancellationToken);

    Error TenantMismatch(RbacPermissionAction action);
}
