using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.DocumentTypeCatalogs.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.DocumentTypeCatalogs;

// ─── Queries ────────────────────────────────────────────────────────────────

public sealed record SearchDocumentTypeCatalogItemsQuery(
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = DocumentTypeCatalogValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<DocumentTypeCatalogItemResponse>>;

public sealed record GetDocumentTypeCatalogItemByIdQuery(
    Guid Id)
    : IQuery<DocumentTypeCatalogItemResponse>;

// ─── Validators ─────────────────────────────────────────────────────────────

internal sealed class SearchDocumentTypeCatalogItemsQueryValidator
    : AbstractValidator<SearchDocumentTypeCatalogItemsQuery>
{
    public SearchDocumentTypeCatalogItemsQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(150);
        RuleFor(q => q.PageNumber).GreaterThan(0);
        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, DocumentTypeCatalogValidationRules.MaxPageSize);
    }
}

internal sealed class GetDocumentTypeCatalogItemByIdQueryValidator
    : AbstractValidator<GetDocumentTypeCatalogItemByIdQuery>
{
    public GetDocumentTypeCatalogItemByIdQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}

// ─── Handlers ───────────────────────────────────────────────────────────────

internal sealed class SearchDocumentTypeCatalogItemsQueryHandler(
    IPlatformAuthorizationService authorizationService,
    IDocumentTypeCatalogRepository repository)
    : IQueryHandler<SearchDocumentTypeCatalogItemsQuery, PagedResponse<DocumentTypeCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<DocumentTypeCatalogItemResponse>>> Handle(
        SearchDocumentTypeCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PagedResponse<DocumentTypeCatalogItemResponse>>.Failure(authResult.Error);
        }

        var response = await repository.SearchAsync(
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<DocumentTypeCatalogItemResponse>>.Success(response);
    }
}

internal sealed class GetDocumentTypeCatalogItemByIdQueryHandler(
    IPlatformAuthorizationService authorizationService,
    IDocumentTypeCatalogRepository repository)
    : IQueryHandler<GetDocumentTypeCatalogItemByIdQuery, DocumentTypeCatalogItemResponse>
{
    public async Task<Result<DocumentTypeCatalogItemResponse>> Handle(
        GetDocumentTypeCatalogItemByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.Id, cancellationToken);
        return response is null
            ? Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound)
            : Result<DocumentTypeCatalogItemResponse>.Success(response);
    }
}
