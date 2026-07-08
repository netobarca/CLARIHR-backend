using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;

namespace CLARIHR.Application.Features.Leave;

internal sealed class LoadLeaveTemplateCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    ILeaveTemplateSeeder leaveTemplateSeeder,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<LoadLeaveTemplateCommand, LeaveTemplateSeedResultResponse>
{
    public async Task<Result<LeaveTemplateSeedResultResponse>> Handle(
        LoadLeaveTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<LeaveTemplateSeedResultResponse>.Failure(authorizationResult.Error);
        }

        // The seeder persists its own template rows; the surrounding transaction makes the
        // template + its audit trail commit atomically (both run on the same unit of work).
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var seedResult = await leaveTemplateSeeder.ApplyTemplateAsync(
                command.CompanyId,
                command.HolidayYear,
                cancellationToken);

            var totalCreated = seedResult.RisksCreated + seedResult.TypesCreated + seedResult.HolidaysCreated;
            var totalSkipped = seedResult.RisksSkipped + seedResult.TypesSkipped + seedResult.HolidaysSkipped;
            var response = new LeaveTemplateSeedResultResponse(
                seedResult.RisksCreated,
                seedResult.RiskParametersCreated,
                seedResult.TypesCreated,
                seedResult.HolidaysCreated,
                seedResult.RisksSkipped,
                seedResult.TypesSkipped,
                seedResult.HolidaysSkipped,
                totalCreated,
                totalSkipped);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.LeaveTemplateLoaded,
                    AuditEntityTypes.LeaveConfiguration,
                    command.CompanyId,
                    "LEAVE_TEMPLATE_SV",
                    AuditActions.Create,
                    $"Loaded the leave-configuration template: {totalCreated} item(s) created, {totalSkipped} skipped.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<LeaveTemplateSeedResultResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
