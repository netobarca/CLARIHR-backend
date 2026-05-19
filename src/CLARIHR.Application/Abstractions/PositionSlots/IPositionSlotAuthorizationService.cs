using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.PositionSlots;

/// <summary>
/// Combined read+manage access decision evaluated in a single pass (one
/// auth/tenant/entitlement check, claim short-circuit, ≤1 permission DB probe in the
/// common paths) so list/detail reads with <c>includeAllowedActions</c> no longer issue
/// a second authorization round-trip per request. See §PS1.
/// </summary>
public readonly record struct PositionSlotAccess(bool CanRead, bool CanManage);

public interface IPositionSlotAuthorizationService
{
    Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken);

    Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves read and manage in one pass. Fails with the same error as
    /// <see cref="EnsureCanReadAsync"/> when the caller cannot even read; otherwise
    /// succeeds with <c>CanRead = true</c> and the resolved <c>CanManage</c>.
    /// </summary>
    Task<Result<PositionSlotAccess>> EvaluateAccessAsync(Guid companyId, CancellationToken cancellationToken);

    Error TenantMismatch(RbacPermissionAction action);
}
