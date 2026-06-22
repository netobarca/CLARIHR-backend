using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Enriched read model for the compensation-concept-type catalog. The generic
/// <see cref="PersonnelCatalogItemResponse"/> cannot carry the default deduction class / calculation
/// type / rates, so this dedicated response surfaces them for the frontend to pre-fill new concepts.
/// </summary>
public sealed record CompensationConceptTypeResponse(
    Guid Id,
    string Code,
    string Name,
    CompensationNature Nature,
    bool IsStatutory,
    DeductionClass? DefaultDeductionClass,
    CompensationCalculationType DefaultCalculationType,
    string? DefaultCalculationBaseCode,
    decimal? DefaultEmployeeRate,
    decimal? DefaultEmployerRate,
    decimal? ContributionCap,
    bool IsActive,
    int SortOrder);

public sealed record GetCompensationConceptTypesQuery(string? CountryCode, CompensationNature? Nature)
    : IQuery<IReadOnlyCollection<CompensationConceptTypeResponse>>;

internal sealed class GetCompensationConceptTypesQueryValidator : AbstractValidator<GetCompensationConceptTypesQuery>
{
    public GetCompensationConceptTypesQueryValidator()
    {
        RuleFor(query => query.CountryCode)
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .When(query => !string.IsNullOrWhiteSpace(query.CountryCode));
        RuleFor(query => query.Nature).IsInEnum().When(query => query.Nature.HasValue);
    }
}

internal sealed class GetCompensationConceptTypesQueryHandler(IPersonnelFileRepository repository)
    : IQueryHandler<GetCompensationConceptTypesQuery, IReadOnlyCollection<CompensationConceptTypeResponse>>
{
    public async Task<Result<IReadOnlyCollection<CompensationConceptTypeResponse>>> Handle(
        GetCompensationConceptTypesQuery query,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetCompensationConceptTypesAsync(query.CountryCode, query.Nature, cancellationToken);
        return Result<IReadOnlyCollection<CompensationConceptTypeResponse>>.Success(items);
    }
}
