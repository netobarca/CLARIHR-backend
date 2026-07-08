using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.Leave;
using FluentValidation;

namespace CLARIHR.Application.Features.Leave;

/// <summary>
/// Summary returned by <c>POST …/leave-configuration/load-template</c>: mirror of
/// <c>LeaveTemplateSeedResult</c> plus created/skipped totals. No <c>ISupportsAllowedActions</c>
/// on purpose — this is a POST-only operation summary, not a listable resource.
/// </summary>
public sealed record LeaveTemplateSeedResultResponse(
    int RisksCreated,
    int RiskParametersCreated,
    int TypesCreated,
    int HolidaysCreated,
    int RisksSkipped,
    int TypesSkipped,
    int HolidaysSkipped,
    int TotalCreated,
    int TotalSkipped);

/// <summary>
/// Applies the El Salvador leave-configuration template to the company (idempotent by code/date:
/// existing rows are skipped, never overwritten). Holidays are only seeded when
/// <paramref name="HolidayYear"/> travels (query param <c>year</c>).
/// </summary>
public sealed record LoadLeaveTemplateCommand(Guid CompanyId, int? HolidayYear)
    : ICommand<LeaveTemplateSeedResultResponse>;

internal sealed class LoadLeaveTemplateCommandValidator : AbstractValidator<LoadLeaveTemplateCommand>
{
    public LoadLeaveTemplateCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.HolidayYear)
            .InclusiveBetween(PayrollPeriodDefinition.MinYear, PayrollPeriodDefinition.MaxYear)
            .When(command => command.HolidayYear.HasValue);
    }
}
