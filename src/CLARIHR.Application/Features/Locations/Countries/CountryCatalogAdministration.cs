using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.Locations.Countries;

public sealed record CountryCatalogItemResponse(
    Guid Id,
    string Code,
    string Name,
    int SortOrder);

public sealed record CountryCatalogLookup(
    long InternalId,
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record GetCountryCatalogItemsQuery()
    : IQuery<IReadOnlyCollection<CountryCatalogItemResponse>>;

internal sealed class GetCountryCatalogItemsQueryHandler(
    ICountryCatalogRepository repository)
    : IQueryHandler<GetCountryCatalogItemsQuery, IReadOnlyCollection<CountryCatalogItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<CountryCatalogItemResponse>>> Handle(
        GetCountryCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetActiveItemsAsync(cancellationToken);
        return Result<IReadOnlyCollection<CountryCatalogItemResponse>>.Success(items);
    }
}
