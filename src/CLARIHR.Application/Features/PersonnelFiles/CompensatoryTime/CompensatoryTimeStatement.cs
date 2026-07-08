using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// One resolved estado-de-cuenta line for the wire: a credit ("ACREDITACION") or an absence ("AUSENCIA"), the
/// signed hours (+ credit / − absence), the movement's type + detail, whether it is annulled (excluded from the
/// balance) and the running fund balance AFTER this movement.
/// </summary>
public sealed record CompensatoryTimeStatementLineResponse(
    Guid MovementPublicId,
    string MovementKind,
    DateOnly Date,
    string CompensatoryTimeTypeCode,
    string TypeNameSnapshot,
    string Detail,
    decimal SignedHours,
    string StatusCode,
    bool IsAnnulled,
    decimal RunningBalance);

/// <summary>
/// The estado de cuenta of the compensatory-time fund (REQ-002 §3.9): the paginated movements with a running
/// balance (computed by <see cref="CompensatoryTime.CompensatoryTimeRules.BuildStatement"/> over the WHOLE
/// filtered set, so a page's running balance already carries the accumulated offset — R-T9), plus the fund
/// totals over the filtered set. With no filters, <see cref="AvailableBalance"/> equals the profile's
/// <c>compensatoryTimeHoursAvailable</c> and <see cref="Common.PersonnelFileErrors"/>-free
/// <c>GetBalanceAsync</c> by construction.
/// </summary>
public sealed record CompensatoryTimeStatementResponse(
    IReadOnlyCollection<CompensatoryTimeStatementLineResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    decimal TotalCredited,
    decimal TotalDebited,
    decimal AvailableBalance);

/// <summary>
/// Repository result of a statement page: the enriched lines of the requested page (running balance already
/// carries the accumulated offset — R-T9), the total count of the filtered set and the fund totals over the
/// whole filtered set.
/// </summary>
public sealed record CompensatoryTimeStatementPage(
    IReadOnlyList<CompensatoryTimeStatementLineResponse> Items,
    int TotalCount,
    decimal TotalCredited,
    decimal TotalDebited,
    decimal AvailableBalance);

public sealed record GetCompensatoryTimeStatementQuery(
    Guid PersonnelFileId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    Guid? CompensatoryTimeTypePublicId,
    string? StatusCode,
    bool IncludeAnnulled,
    int PageNumber,
    int PageSize)
    : IQuery<CompensatoryTimeStatementResponse>;

internal sealed class GetCompensatoryTimeStatementQueryValidator : AbstractValidator<GetCompensatoryTimeStatementQuery>
{
    public GetCompensatoryTimeStatementQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, Common.PersonnelFileValidationRules.MaxPageSize);
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate!.Value)
            .When(query => query is { FromDate: not null, ToDate: not null });
    }
}
