using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.Payroll;

/// <summary>
/// Summary returned by <c>POST …/payroll-configuration/load-template</c>. No
/// <c>ISupportsAllowedActions</c> on purpose — this is a POST-only operation summary, not a listable
/// resource (mirrors <c>OvertimeTemplateSeedResultResponse</c>).
/// </summary>
public sealed record PayrollConfigurationTemplateSeedResultResponse(
    int WorkSchedulesCreated,
    int WorkSchedulesSkipped);

/// <summary>
/// Applies the El Salvador payroll-configuration template to the company (today: the 44-hour legal work
/// schedule — golden 11). Idempotent by normalized code: existing rows are skipped, never overwritten.
/// </summary>
public sealed record LoadPayrollConfigurationTemplateCommand(Guid CompanyId)
    : ICommand<PayrollConfigurationTemplateSeedResultResponse>;

internal sealed class LoadPayrollConfigurationTemplateCommandValidator
    : AbstractValidator<LoadPayrollConfigurationTemplateCommand>
{
    public LoadPayrollConfigurationTemplateCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
    }
}

internal sealed class LoadPayrollConfigurationTemplateCommandHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IWorkScheduleTemplateSeeder templateSeeder,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<LoadPayrollConfigurationTemplateCommand, PayrollConfigurationTemplateSeedResultResponse>
{
    public async Task<Result<PayrollConfigurationTemplateSeedResultResponse>> Handle(
        LoadPayrollConfigurationTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollConfigurationTemplateSeedResultResponse>.Failure(authorizationResult.Error);
        }

        // The seeder persists its own template rows; the surrounding transaction makes the template + its
        // audit trail commit atomically (both run on the same unit of work).
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var seedResult = await templateSeeder.ApplyTemplateAsync(command.CompanyId, cancellationToken);

            var response = new PayrollConfigurationTemplateSeedResultResponse(
                seedResult.WorkSchedulesCreated,
                seedResult.WorkSchedulesSkipped);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollConfigurationTemplateLoaded,
                    AuditEntityTypes.PayrollConfiguration,
                    command.CompanyId,
                    "PAYROLL_CONFIGURATION_TEMPLATE_SV",
                    AuditActions.Create,
                    $"Loaded the payroll configuration template: {seedResult.WorkSchedulesCreated} work schedule(s) created, {seedResult.WorkSchedulesSkipped} skipped.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollConfigurationTemplateSeedResultResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
