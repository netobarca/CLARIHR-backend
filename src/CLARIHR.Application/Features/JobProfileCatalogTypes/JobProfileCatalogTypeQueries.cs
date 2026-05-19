using CLARIHR.Application.Abstractions.CatalogTypes;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfileCatalogTypes.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfileCatalogTypes;

// ─── Queries ────────────────────────────────────────────────────────────────

public sealed record SearchJobProfileCatalogTypesQuery(
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = JobProfileCatalogTypeValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<JobProfileCatalogTypeResponse>>;

public sealed record GetJobProfileCatalogTypeByIdQuery(
    Guid Id)
    : IQuery<JobProfileCatalogTypeResponse>;

// ─── Validators ─────────────────────────────────────────────────────────────

internal sealed class SearchJobProfileCatalogTypesQueryValidator
    : AbstractValidator<SearchJobProfileCatalogTypesQuery>
{
    public SearchJobProfileCatalogTypesQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(150);
        RuleFor(q => q.PageNumber).GreaterThan(0);
        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, JobProfileCatalogTypeValidationRules.MaxPageSize);
    }
}

internal sealed class GetJobProfileCatalogTypeByIdQueryValidator
    : AbstractValidator<GetJobProfileCatalogTypeByIdQuery>
{
    public GetJobProfileCatalogTypeByIdQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
    }
}

// ─── Handlers ───────────────────────────────────────────────────────────────

internal sealed class SearchJobProfileCatalogTypesQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICatalogTypeDescriptorRepository repository)
    : IQueryHandler<SearchJobProfileCatalogTypesQuery, PagedResponse<JobProfileCatalogTypeResponse>>
{
    public async Task<Result<PagedResponse<JobProfileCatalogTypeResponse>>> Handle(
        SearchJobProfileCatalogTypesQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<PagedResponse<JobProfileCatalogTypeResponse>>.Failure(authResult.Error);
        }

        var response = await repository.SearchAsync(
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<JobProfileCatalogTypeResponse>>.Success(response);
    }
}

internal sealed class GetJobProfileCatalogTypeByIdQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICatalogTypeDescriptorRepository repository)
    : IQueryHandler<GetJobProfileCatalogTypeByIdQuery, JobProfileCatalogTypeResponse>
{
    public async Task<Result<JobProfileCatalogTypeResponse>> Handle(
        GetJobProfileCatalogTypeByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(authResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.Id, cancellationToken);
        return response is null
            ? Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound)
            : Result<JobProfileCatalogTypeResponse>.Success(response);
    }
}
