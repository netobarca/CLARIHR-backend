using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Enriched read model for the contract-type catalog (RF-011). The generic
/// <see cref="PersonnelCatalogItemResponse"/> cannot carry the abbreviation / temporary flag, so this
/// dedicated response surfaces them (mirror of <see cref="CompensationConceptTypeResponse"/>).
/// </summary>
public sealed record ContractTypeResponse(
    Guid Id,
    string Code,
    string Name,
    string? Abbreviation,
    bool IsTemporary,
    bool IsActive,
    int SortOrder);

public sealed record GetContractTypesQuery(string? CountryCode)
    : IQuery<IReadOnlyCollection<ContractTypeResponse>>;

internal sealed class GetContractTypesQueryValidator : AbstractValidator<GetContractTypesQuery>
{
    public GetContractTypesQueryValidator()
    {
        RuleFor(query => query.CountryCode)
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .When(query => !string.IsNullOrWhiteSpace(query.CountryCode));
    }
}

internal sealed class GetContractTypesQueryHandler(IPersonnelFileRepository repository)
    : IQueryHandler<GetContractTypesQuery, IReadOnlyCollection<ContractTypeResponse>>
{
    public async Task<Result<IReadOnlyCollection<ContractTypeResponse>>> Handle(
        GetContractTypesQuery query,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetContractTypesAsync(query.CountryCode, cancellationToken);
        return Result<IReadOnlyCollection<ContractTypeResponse>>.Success(items);
    }
}
