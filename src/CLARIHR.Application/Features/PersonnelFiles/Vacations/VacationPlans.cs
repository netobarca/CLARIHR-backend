using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.Leave;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// One non-blocking warning attached to a plan line (leave module §3.7, aclaración №9): a bilingual code +
/// message. The annual plan is indicative (D-24) so availability / holiday / rest-day findings are surfaced as
/// warnings instead of 422s.
/// </summary>
public sealed record VacationPlanLineWarning(string Code, string Message);

/// <summary>One planned vacation window of a plan (employee + dates + days) with its computed warnings.</summary>
public sealed record VacationPlanLineResponse(
    Guid PersonnelFilePublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days,
    int SortOrder,
    IReadOnlyList<VacationPlanLineWarning> Warnings);

/// <summary>
/// A company-level yearly vacation plan (leave module §3.7, D-24): the indicative schedule of intended vacation
/// windows per employee for one year. VIGENTE or ANULADO. The warnings on each line are populated on the POST/PUT
/// responses (availability / holiday / rest-day); plain reads carry empty warning lists.
/// </summary>
public sealed record VacationPlanResponse(
    Guid VacationPlanPublicId,
    int PlanYear,
    DateOnly RequestDate,
    string? RequesterNameSnapshot,
    string StatusCode,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    IReadOnlyList<VacationPlanLineResponse> Lines)
{
    [JsonIgnore]
    public Guid Id => VacationPlanPublicId;
}

/// <summary>One planned line supplied when creating / replacing a plan (employee publicId + dates + days).</summary>
public sealed record VacationPlanLineItem(
    Guid PersonnelFilePublicId,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days);

public sealed record AddVacationPlanCommand(
    Guid CompanyId,
    int PlanYear,
    IReadOnlyCollection<VacationPlanLineItem> Lines)
    : ICommand<VacationPlanResponse>;

public sealed record UpdateVacationPlanCommand(
    Guid CompanyId,
    Guid VacationPlanPublicId,
    IReadOnlyCollection<VacationPlanLineItem> Lines,
    Guid ConcurrencyToken)
    : ICommand<VacationPlanResponse>;

public sealed record AnnulVacationPlanCommand(
    Guid CompanyId,
    Guid VacationPlanPublicId,
    Guid ConcurrencyToken)
    : ICommand<VacationPlanResponse>;

public sealed record GetVacationPlansQuery(Guid CompanyId, int? Year)
    : IQuery<IReadOnlyCollection<VacationPlanResponse>>;

public sealed record GetVacationPlanByIdQuery(Guid CompanyId, Guid VacationPlanPublicId)
    : IQuery<VacationPlanResponse>;

/// <summary>Bilingual warning codes surfaced per plan line (aclaración №9). Localized via the message catalog.</summary>
public static class VacationPlanWarnings
{
    public const string InsufficientFundCode = "VACATION_PLAN_WARNING_INSUFFICIENT_FUND";
    public const string DateRuleCode = "VACATION_PLAN_WARNING_DATE_RULE";

    public const string InsufficientFundDefaultMessage =
        "The planned vacation exceeds the days available in the employee's fund (indicative — not blocking).";

    public const string DateRuleDefaultMessage =
        "The planned vacation starts on a holiday / the employee's rest day or ends on a holiday (indicative — not blocking).";
}

/// <summary>
/// Pure plan-line arithmetic (leave module §3.7): the same-employee overlap detection surfaced by the handler as
/// a 422 (VACATION_PLAN_LINE_OVERLAP) before the domain guard fires — a clean 422 instead of a 500 (the guard
/// remains the invariant).
/// </summary>
public static class VacationPlanRules
{
    /// <summary>True when two lines of the same employee overlap (a start on or before a previous end).</summary>
    public static bool HasOverlappingLines(IEnumerable<VacationPlanLineItem> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        foreach (var employeeLines in lines.GroupBy(line => line.PersonnelFilePublicId))
        {
            var ordered = employeeLines.OrderBy(line => line.StartDate).ThenBy(line => line.EndDate).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                if (ordered[index].StartDate <= ordered[index - 1].EndDate)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>Maps a <see cref="VacationPlan"/> aggregate to its wire response, attaching per-line warnings by line public id.</summary>
public static class VacationPlanMapping
{
    private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<VacationPlanLineWarning>> NoWarnings =
        new Dictionary<Guid, IReadOnlyList<VacationPlanLineWarning>>();

    public static VacationPlanResponse Map(
        VacationPlan plan,
        IReadOnlyDictionary<Guid, IReadOnlyList<VacationPlanLineWarning>>? warningsByLinePublicId = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var warnings = warningsByLinePublicId ?? NoWarnings;
        var lines = plan.Lines
            .OrderBy(line => line.SortOrder)
            .Select(line => new VacationPlanLineResponse(
                line.PersonnelFilePublicId,
                line.StartDate,
                line.EndDate,
                line.Days,
                line.SortOrder,
                warnings.TryGetValue(line.PublicId, out var lineWarnings) ? lineWarnings : []))
            .ToArray();

        return new VacationPlanResponse(
            plan.PublicId,
            plan.PlanYear,
            plan.RequestDate,
            plan.RequesterNameSnapshot,
            plan.StatusCode,
            plan.IsActive,
            plan.ConcurrencyToken,
            plan.CreatedUtc,
            plan.ModifiedUtc,
            lines);
    }
}

// ── Validators ─────────────────────────────────────────────────────────────────────────────────

internal sealed class VacationPlanLineItemValidator : AbstractValidator<VacationPlanLineItem>
{
    public VacationPlanLineItemValidator()
    {
        RuleFor(line => line.PersonnelFilePublicId).NotEmpty();
        RuleFor(line => line.StartDate).NotEmpty();
        RuleFor(line => line.EndDate).GreaterThanOrEqualTo(line => line.StartDate);
        RuleFor(line => line.Days).GreaterThan(0);
    }
}

internal sealed class AddVacationPlanCommandValidator : AbstractValidator<AddVacationPlanCommand>
{
    public AddVacationPlanCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PlanYear).InclusiveBetween(2000, 2100);
        RuleFor(command => command.Lines).NotNull();
        RuleForEach(command => command.Lines).SetValidator(new VacationPlanLineItemValidator());
    }
}

internal sealed class UpdateVacationPlanCommandValidator : AbstractValidator<UpdateVacationPlanCommand>
{
    public UpdateVacationPlanCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.VacationPlanPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Lines).NotNull();
        RuleForEach(command => command.Lines).SetValidator(new VacationPlanLineItemValidator());
    }
}

internal sealed class AnnulVacationPlanCommandValidator : AbstractValidator<AnnulVacationPlanCommand>
{
    public AnnulVacationPlanCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.VacationPlanPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetVacationPlansQueryValidator : AbstractValidator<GetVacationPlansQuery>
{
    public GetVacationPlansQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Year).InclusiveBetween(2000, 2100).When(query => query.Year.HasValue);
    }
}

internal sealed class GetVacationPlanByIdQueryValidator : AbstractValidator<GetVacationPlanByIdQuery>
{
    public GetVacationPlanByIdQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.VacationPlanPublicId).NotEmpty();
    }
}
