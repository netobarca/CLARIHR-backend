using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Enriched read model for the AFP master catalog (RF-007). The generic
/// <see cref="PersonnelCatalogItemResponse"/> cannot carry the identity/contact columns, so this
/// dedicated response surfaces them (mirror of <see cref="CompensationConceptTypeResponse"/>).
/// </summary>
public sealed record AfpResponse(
    Guid Id,
    string Code,
    string Name,
    string? Abbreviation,
    string? Address,
    string? Phone,
    string? Fax,
    string? ContactName,
    bool IsActive,
    int SortOrder);

public sealed record GetAfpsQuery(string? CountryCode)
    : IQuery<IReadOnlyCollection<AfpResponse>>;

internal sealed class GetAfpsQueryValidator : AbstractValidator<GetAfpsQuery>
{
    public GetAfpsQueryValidator()
    {
        RuleFor(query => query.CountryCode)
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .When(query => !string.IsNullOrWhiteSpace(query.CountryCode));
    }
}

internal sealed class GetAfpsQueryHandler(IPersonnelFileRepository repository)
    : IQueryHandler<GetAfpsQuery, IReadOnlyCollection<AfpResponse>>
{
    public async Task<Result<IReadOnlyCollection<AfpResponse>>> Handle(
        GetAfpsQuery query,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetAfpsAsync(query.CountryCode, cancellationToken);
        return Result<IReadOnlyCollection<AfpResponse>>.Success(items);
    }
}
