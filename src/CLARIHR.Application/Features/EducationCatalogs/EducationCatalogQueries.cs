using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.EducationCatalogs;

// ─── Queries ────────────────────────────────────────────────────────────────

public sealed record SearchEducationCatalogItemsQuery(
    EducationCatalogType CatalogType,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = EducationCatalogValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<EducationCatalogItemResponse>>;

public sealed record GetEducationCatalogItemByIdQuery(
    EducationCatalogType CatalogType,
    Guid Id)
    : IQuery<EducationCatalogItemResponse>;

// ─── Validators ─────────────────────────────────────────────────────────────

internal sealed class SearchEducationCatalogItemsQueryValidator
    : AbstractValidator<SearchEducationCatalogItemsQuery>
{
    public SearchEducationCatalogItemsQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(150);
        RuleFor(q => q.PageNumber).GreaterThan(0);
        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, EducationCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class GetEducationCatalogItemByIdQueryValidator
    : AbstractValidator<GetEducationCatalogItemByIdQuery>
{
    public GetEducationCatalogItemByIdQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}

// ─── Handlers ───────────────────────────────────────────────────────────────

internal sealed class SearchEducationCatalogItemsQueryHandler(
    IPlatformAuthorizationService authorizationService,
    IEducationCatalogRepository repository)
    : IQueryHandler<SearchEducationCatalogItemsQuery, PagedResponse<EducationCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<EducationCatalogItemResponse>>> Handle(
        SearchEducationCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PagedResponse<EducationCatalogItemResponse>>.Failure(authResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CatalogType,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<EducationCatalogItemResponse>>.Success(response);
    }
}

internal sealed class GetEducationCatalogItemByIdQueryHandler(
    IPlatformAuthorizationService authorizationService,
    IEducationCatalogRepository repository)
    : IQueryHandler<GetEducationCatalogItemByIdQuery, EducationCatalogItemResponse>
{
    public async Task<Result<EducationCatalogItemResponse>> Handle(
        GetEducationCatalogItemByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<EducationCatalogItemResponse>.Failure(authResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.CatalogType, query.Id, cancellationToken);
        return response is null
            ? Result<EducationCatalogItemResponse>.Failure(EducationCatalogErrors.NotFound)
            : Result<EducationCatalogItemResponse>.Success(response);
    }
}
