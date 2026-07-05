using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Enriched read model for the settlement-concept catalog (D-07). The generic
/// <see cref="PersonnelCatalogItemResponse"/> cannot carry the concept class / affectation matrix /
/// exemption rule / employer rate, so this dedicated response surfaces them for the frontend
/// (line pickers, section grouping) and mirrors <see cref="CompensationConceptTypeResponse"/>.
/// </summary>
public sealed record SettlementConceptResponse(
    Guid Id,
    string Code,
    string Name,
    SettlementConceptClass ConceptClass,
    bool AffectsIsss,
    bool AffectsAfp,
    bool AffectsRenta,
    SettlementExemptionRule ExemptionRule,
    decimal? ExemptionMultiplier,
    bool IsSystemCalculated,
    decimal? DefaultRatePercent,
    bool IsActive,
    int SortOrder);

public sealed record GetSettlementConceptsQuery(string? CountryCode, SettlementConceptClass? ConceptClass)
    : IQuery<IReadOnlyCollection<SettlementConceptResponse>>;

internal sealed class GetSettlementConceptsQueryValidator : AbstractValidator<GetSettlementConceptsQuery>
{
    public GetSettlementConceptsQueryValidator()
    {
        RuleFor(query => query.CountryCode)
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .When(query => !string.IsNullOrWhiteSpace(query.CountryCode));
        RuleFor(query => query.ConceptClass).IsInEnum().When(query => query.ConceptClass.HasValue);
    }
}

internal sealed class GetSettlementConceptsQueryHandler(IPersonnelFileRepository repository)
    : IQueryHandler<GetSettlementConceptsQuery, IReadOnlyCollection<SettlementConceptResponse>>
{
    public async Task<Result<IReadOnlyCollection<SettlementConceptResponse>>> Handle(
        GetSettlementConceptsQuery query,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetSettlementConceptsAsync(query.CountryCode, query.ConceptClass, cancellationToken);
        return Result<IReadOnlyCollection<SettlementConceptResponse>>.Success(items);
    }
}
