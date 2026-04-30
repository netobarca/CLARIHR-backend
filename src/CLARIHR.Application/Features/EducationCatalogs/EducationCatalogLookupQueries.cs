using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.EducationCatalogs;

// ─── Lookup Queries (Public API — CLARIHR Core) ──────────────────────────────

/// <summary>
/// Paged search of active and inactive education catalog items for display in CLARIHR Core forms.
/// No Backoffice authorization required — any authenticated tenant user can read these.
/// </summary>
public sealed record SearchEducationCatalogLookupQuery(
    EducationCatalogType CatalogType,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = EducationCatalogValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<EducationCatalogLookup>>;

/// <summary>
/// Fetch a single active education catalog item by Id for display/validation in CLARIHR Core.
/// </summary>
public sealed record GetEducationCatalogActiveLookupByIdQuery(
    EducationCatalogType CatalogType,
    Guid Id)
    : IQuery<EducationCatalogLookup>;

// ─── Validators ──────────────────────────────────────────────────────────────

internal sealed class SearchEducationCatalogLookupQueryValidator
    : AbstractValidator<SearchEducationCatalogLookupQuery>
{
    public SearchEducationCatalogLookupQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(150);
        RuleFor(q => q.PageNumber).GreaterThan(0);
        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, EducationCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class GetEducationCatalogActiveLookupByIdQueryValidator
    : AbstractValidator<GetEducationCatalogActiveLookupByIdQuery>
{
    public GetEducationCatalogActiveLookupByIdQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}

// ─── Handlers ────────────────────────────────────────────────────────────────

internal sealed class SearchEducationCatalogLookupQueryHandler(
    IEducationCatalogRepository repository)
    : IQueryHandler<SearchEducationCatalogLookupQuery, PagedResponse<EducationCatalogLookup>>
{
    public async Task<Result<PagedResponse<EducationCatalogLookup>>> Handle(
        SearchEducationCatalogLookupQuery query,
        CancellationToken cancellationToken)
    {
        // Map to full response then project to lookup
        var fullResponse = await repository.SearchAsync(
            query.CatalogType,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var lookups = fullResponse.Items
            .Select(item => new EducationCatalogLookup(0, item.Id, item.Code, item.Name, item.IsActive))
            .ToList();

        var response = new PagedResponse<EducationCatalogLookup>(
            lookups,
            fullResponse.PageNumber,
            fullResponse.PageSize,
            fullResponse.TotalCount);

        return Result<PagedResponse<EducationCatalogLookup>>.Success(response);
    }
}

internal sealed class GetEducationCatalogActiveLookupByIdQueryHandler(
    IEducationCatalogRepository repository)
    : IQueryHandler<GetEducationCatalogActiveLookupByIdQuery, EducationCatalogLookup>
{
    public async Task<Result<EducationCatalogLookup>> Handle(
        GetEducationCatalogActiveLookupByIdQuery query,
        CancellationToken cancellationToken)
    {
        var lookup = await repository.GetActiveLookupByIdAsync(query.CatalogType, query.Id, cancellationToken);
        return lookup is null
            ? Result<EducationCatalogLookup>.Failure(EducationCatalogErrors.NotFound)
            : Result<EducationCatalogLookup>.Success(lookup);
    }
}
