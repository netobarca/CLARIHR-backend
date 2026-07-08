using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Abstractions.Leave;

public interface ICompanyHolidayRepository
{
    void Add(CompanyHoliday companyHoliday);

    Task<CompanyHoliday?> GetByIdAsync(Guid companyHolidayId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid companyHolidayId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the (TenantId, Date) unique key. Pass the public id of the holiday being
    /// edited in <paramref name="excludingCompanyHolidayId"/> to exclude itself.
    /// </summary>
    Task<bool> DateExistsAsync(
        Guid tenantId,
        DateOnly date,
        Guid? excludingCompanyHolidayId,
        CancellationToken cancellationToken);

    Task<PagedResponse<CompanyHolidayListItemResponse>> SearchAsync(
        Guid tenantId,
        int? year,
        string? scopeCode,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CompanyHolidayResponse?> GetResponseByIdAsync(Guid companyHolidayId, CancellationToken cancellationToken);
}
