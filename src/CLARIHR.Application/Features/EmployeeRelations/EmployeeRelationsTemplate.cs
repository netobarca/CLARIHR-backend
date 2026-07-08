using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.EmployeeRelations;

/// <summary>
/// Summary returned by <c>POST …/employee-relations/load-template</c>: mirror of
/// <c>EmployeeRelationsTemplateSeedResult</c> plus created/skipped totals. No
/// <c>ISupportsAllowedActions</c> on purpose — this is a POST-only operation summary, not a listable
/// resource.
/// </summary>
public sealed record EmployeeRelationsTemplateSeedResultResponse(
    int RecognitionTypesCreated,
    int DisciplinaryActionTypesCreated,
    int DisciplinaryActionCausesCreated,
    int RecognitionTypesSkipped,
    int DisciplinaryActionTypesSkipped,
    int DisciplinaryActionCausesSkipped,
    int TotalCreated,
    int TotalSkipped);

/// <summary>
/// Applies the El Salvador employee-relations configuration template to the company (idempotent by
/// normalized code: existing rows are skipped, never overwritten).
/// </summary>
public sealed record LoadEmployeeRelationsTemplateCommand(Guid CompanyId)
    : ICommand<EmployeeRelationsTemplateSeedResultResponse>;

internal sealed class LoadEmployeeRelationsTemplateCommandValidator : AbstractValidator<LoadEmployeeRelationsTemplateCommand>
{
    public LoadEmployeeRelationsTemplateCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
    }
}
