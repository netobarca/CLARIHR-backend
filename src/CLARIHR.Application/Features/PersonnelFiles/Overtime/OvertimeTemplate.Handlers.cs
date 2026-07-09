using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Overtime;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;

namespace CLARIHR.Application.Features.PersonnelFiles.Overtime;

internal sealed class LoadOvertimeTemplateCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IOvertimeTemplateSeeder templateSeeder,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<LoadOvertimeTemplateCommand, OvertimeTemplateSeedResultResponse>
{
    public async Task<Result<OvertimeTemplateSeedResultResponse>> Handle(
        LoadOvertimeTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageOvertimeRecordsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeTemplateSeedResultResponse>.Failure(authorizationResult.Error);
        }

        // The seeder persists its own template rows; the surrounding transaction makes the template + its
        // audit trail commit atomically (both run on the same unit of work).
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var seedResult = await templateSeeder.ApplyTemplateAsync(command.CompanyId, cancellationToken);

            var totalCreated = seedResult.OvertimeTypesCreated + seedResult.OvertimeJustificationTypesCreated;
            var totalSkipped = seedResult.OvertimeTypesSkipped + seedResult.OvertimeJustificationTypesSkipped;
            var response = new OvertimeTemplateSeedResultResponse(
                seedResult.OvertimeTypesCreated,
                seedResult.OvertimeJustificationTypesCreated,
                seedResult.OvertimeTypesSkipped,
                seedResult.OvertimeJustificationTypesSkipped,
                totalCreated,
                totalSkipped);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OvertimeTemplateLoaded,
                    AuditEntityTypes.OvertimeConfiguration,
                    command.CompanyId,
                    "OVERTIME_TEMPLATE_SV",
                    AuditActions.Create,
                    $"Loaded the overtime configuration template: {totalCreated} item(s) created, {totalSkipped} skipped.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OvertimeTemplateSeedResultResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
