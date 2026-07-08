using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.EmployeeRelations;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;

namespace CLARIHR.Application.Features.EmployeeRelations;

internal sealed class LoadEmployeeRelationsTemplateCommandHandler(
    IEmployeeRelationsConfigurationAuthorizationService authorizationService,
    IEmployeeRelationsTemplateSeeder templateSeeder,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<LoadEmployeeRelationsTemplateCommand, EmployeeRelationsTemplateSeedResultResponse>
{
    public async Task<Result<EmployeeRelationsTemplateSeedResultResponse>> Handle(
        LoadEmployeeRelationsTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<EmployeeRelationsTemplateSeedResultResponse>.Failure(authorizationResult.Error);
        }

        // The seeder persists its own template rows; the surrounding transaction makes the
        // template + its audit trail commit atomically (both run on the same unit of work).
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var seedResult = await templateSeeder.ApplyTemplateAsync(command.CompanyId, cancellationToken);

            var totalCreated = seedResult.RecognitionTypesCreated
                + seedResult.DisciplinaryActionTypesCreated
                + seedResult.DisciplinaryActionCausesCreated;
            var totalSkipped = seedResult.RecognitionTypesSkipped
                + seedResult.DisciplinaryActionTypesSkipped
                + seedResult.DisciplinaryActionCausesSkipped;
            var response = new EmployeeRelationsTemplateSeedResultResponse(
                seedResult.RecognitionTypesCreated,
                seedResult.DisciplinaryActionTypesCreated,
                seedResult.DisciplinaryActionCausesCreated,
                seedResult.RecognitionTypesSkipped,
                seedResult.DisciplinaryActionTypesSkipped,
                seedResult.DisciplinaryActionCausesSkipped,
                totalCreated,
                totalSkipped);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.EmployeeRelationsTemplateLoaded,
                    AuditEntityTypes.EmployeeRelationsConfiguration,
                    command.CompanyId,
                    "EMPLOYEE_RELATIONS_TEMPLATE_SV",
                    AuditActions.Create,
                    $"Loaded the employee-relations configuration template: {totalCreated} item(s) created, {totalSkipped} skipped.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<EmployeeRelationsTemplateSeedResultResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
