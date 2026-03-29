using CLARIHR.Application.Features.Locations.Countries;

namespace CLARIHR.Application.Abstractions.Locations;

public interface ICountryCatalogRepository
{
    Task<IReadOnlyCollection<CountryCatalogItemResponse>> GetActiveItemsAsync(CancellationToken cancellationToken);

    Task<CountryCatalogLookup?> GetActiveByCodeAsync(string countryCode, CancellationToken cancellationToken);
}
