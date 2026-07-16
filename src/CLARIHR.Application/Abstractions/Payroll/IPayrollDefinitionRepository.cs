using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Domain.Payroll;

namespace CLARIHR.Application.Abstractions.Payroll;

public interface IPayrollDefinitionRepository
{
    void Add(PayrollDefinition definition);

    Task<PayrollDefinition?> GetByIdAsync(Guid payrollDefinitionId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid payrollDefinitionId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the filtered (TenantId, NormalizedCode) WHERE is_active unique key. Only ACTIVE
    /// definitions conflict; pass the public id of the definition being edited in
    /// <paramref name="excludingPayrollDefinitionId"/> to exclude itself.
    /// </summary>
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingPayrollDefinitionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the payroll is referenced by an active period or a live run (logical inactivation is blocked
    /// with <c>PAYROLL_DEFINITION_IN_USE</c>). PR-1 has neither table/column yet (periods gain their FK in
    /// M2/PR-2, runs arrive in M4/PR-4), so this always returns <c>false</c> today; the real reference
    /// probes are wired as those PRs land.
    /// </summary>
    Task<bool> IsInUseAsync(Guid tenantId, Guid payrollDefinitionId, CancellationToken cancellationToken);

    Task<PagedResponse<PayrollDefinitionListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PayrollDefinitionResponse?> GetResponseByIdAsync(Guid payrollDefinitionId, CancellationToken cancellationToken);
}
