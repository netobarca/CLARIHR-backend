using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialAddons;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICommercialAddonRepository
{
    void Add(CommercialAddon addon);

    Task<CommercialAddon?> GetByIdAsync(Guid commercialAddonId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string normalizedCode, long? excludingId, CancellationToken cancellationToken);

    Task<PagedResponse<CommercialAddonSummaryResponse>> SearchAsync(
        CommercialAddonStatus? status,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}
