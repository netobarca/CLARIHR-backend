using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.Overtime;

/// <summary>
/// Summary returned by <c>POST …/overtime-configuration/load-template</c>: mirror of
/// <c>OvertimeTemplateSeedResult</c> plus created/skipped totals. No <c>ISupportsAllowedActions</c> on
/// purpose — this is a POST-only operation summary, not a listable resource.
/// </summary>
public sealed record OvertimeTemplateSeedResultResponse(
    int OvertimeTypesCreated,
    int OvertimeJustificationTypesCreated,
    int OvertimeTypesSkipped,
    int OvertimeJustificationTypesSkipped,
    int TotalCreated,
    int TotalSkipped);

/// <summary>
/// Applies the El Salvador overtime configuration template to the company (idempotent by normalized code:
/// existing rows are skipped, never overwritten).
/// </summary>
public sealed record LoadOvertimeTemplateCommand(Guid CompanyId)
    : ICommand<OvertimeTemplateSeedResultResponse>;

internal sealed class LoadOvertimeTemplateCommandValidator : AbstractValidator<LoadOvertimeTemplateCommand>
{
    public LoadOvertimeTemplateCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
    }
}
