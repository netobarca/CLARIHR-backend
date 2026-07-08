using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Abstractions.Leave;

public interface IPayrollPeriodRepository
{
    void Add(PayrollPeriodDefinition payrollPeriod);

    Task<PayrollPeriodDefinition?> GetByIdAsync(Guid payrollPeriodId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid payrollPeriodId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the (TenantId, PayPeriodTypeCode, Year, Number) unique key. Pass the
    /// public id of the period being edited in <paramref name="excludingPayrollPeriodId"/> to
    /// exclude itself.
    /// </summary>
    Task<bool> PeriodExistsAsync(
        Guid tenantId,
        string payPeriodTypeCode,
        int year,
        int number,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken);

    /// <summary>
    /// True when the [<paramref name="startDate"/>, <paramref name="endDate"/>] range overlaps
    /// another ACTIVE period of the same pay-period type and year
    /// (<c>start &lt;= other.End &amp;&amp; end &gt;= other.Start</c>).
    /// </summary>
    Task<bool> HasOverlapAsync(
        Guid tenantId,
        string payPeriodTypeCode,
        int year,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken);

    Task<PagedResponse<PayrollPeriodListItemResponse>> SearchAsync(
        Guid tenantId,
        string? payPeriodTypeCode,
        int? year,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PayrollPeriodResponse?> GetResponseByIdAsync(Guid payrollPeriodId, CancellationToken cancellationToken);
}
