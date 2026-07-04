using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Reversal of an EJECUTADA retirement (RF-010…RF-012, D-09/D-10/D-11 + the ratified 30-day window
/// RN-012.4): one transaction that restores EXACTLY the execution snapshot — profile metadata cleared and
/// PRIOR employment status restored (never assumes ACTIVO), file reactivated with its prior rehire-block
/// state, the closed plaza/contract rows reopened with their PREVIOUS end dates, the login reactivated only
/// if it was active before, the exit-interview submissions archived (they must not count in rotation
/// analytics) and the REVERSION_BAJA action journaled. Seniority resumes CONTINUOUS (HireDate never moved) —
/// the key distinction from a rehire, which opens a NEW period.
/// </summary>
public sealed record RevertRetirementRequestCommand(
    Guid PersonnelFileId,
    Guid RetirementRequestPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileRetirementRequestResponse>;

internal sealed class RevertRetirementRequestCommandValidator : AbstractValidator<RevertRetirementRequestCommand>
{
    public RevertRetirementRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.RetirementRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(2000);
    }
}

internal sealed class RevertRetirementRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IExitInterviewRepository exitInterviewRepository,
    ICompanyUserLifecycleService companyUserLifecycleService,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<RevertRetirementRequestCommand, PersonnelFileRetirementRequestResponse>
{
    private const string ReversalActionTypeCode = "REVERSION_BAJA";
    private const string AppliedActionStatusCode = "APLICADA";
    private const string RehireActionTypeCode = "RECONTRATACION";

    public async Task<Result<PersonnelFileRetirementRequestResponse>> Handle(
        RevertRetirementRequestCommand command,
        CancellationToken cancellationToken)
    {
        // Dedicated grant (D-12) — PersonnelFiles.Admin deliberately does NOT imply it.
        var (failure, personnelFile) = await LoadForRevertRetirementAsync<PersonnelFileRetirementRequestResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var tenantId = personnelFile!.TenantId;
        var entity = await employeeRepository.GetRetirementRequestEntityAsync(
            personnelFile.PublicId, command.RetirementRequestPublicId, tenantId, includeClosedRecords: true, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.RequestStatusCode != RetirementRequestStatuses.Ejecutada || entity.ExecutionDateUtc is not { } executionDateUtc)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.StateRuleViolation);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.ReversalReasonRequired);
        }

        // D-13: the subject employee never reverts their own baja.
        _ = Guid.TryParse(currentUserService.UserId, out var revertedByUserId);
        if (personnelFile.LinkedUserPublicId is { } subjectUserId && subjectUserId == revertedByUserId)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.SelfActionForbidden);
        }

        // RN-012.4 (ratified): exact-timestamp 30-day window anchored on the execution.
        var nowUtc = dateTimeProvider.UtcNow;
        if (!RetirementRequestRules.IsWithinReversalWindow(executionDateUtc, nowUtc))
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.ReversalWindowExpired);
        }

        // D-10: a rehire after the execution closed the period — reverting would corrupt the timeline.
        if (await employeeRepository.HasPersonnelActionSinceAsync(personnelFile.Id, tenantId, RehireActionTypeCode, executionDateUtc, cancellationToken))
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.ReversalBlockedByRehire);
        }

        // RN-012.3: only the most recent executed retirement can be reverted.
        if (await employeeRepository.HasLaterExecutedRetirementRequestAsync(personnelFile.Id, tenantId, entity.PublicId, executionDateUtc, cancellationToken))
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.ReversalNotMostRecent);
        }

        // RN-012.2: the current state must still match what the execution left (retired profile with the
        // request's exact codes/date, inactive file) — otherwise restoring would overwrite modified data.
        var profile = await employeeRepository.GetEmployeeProfileEntityAsync(personnelFile.Id, tenantId, cancellationToken);
        var diverged = profile is null
            || profile.RetirementDate != entity.RetirementDate
            || !string.Equals(profile.RetirementReasonCode, entity.RetirementReasonCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(profile.RetirementCategoryCode, entity.RetirementCategoryCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(profile.EmploymentStatusCode, PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode, StringComparison.OrdinalIgnoreCase)
            || personnelFile.IsActive;
        if (diverged)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.ReversalStateDiverged);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // [1] Profile: clear the baja and restore the PRIOR employment status from the snapshot (D-11).
            profile!.ClearRetirement(entity.PriorEmploymentStatusCode ?? PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode);

            // [2] File: active again; the rehire-block returns to its pre-execution state.
            personnelFile.Activate();
            if (entity.PriorRehireBlocked == true)
            {
                personnelFile.BlockRehire(entity.PriorRehireBlockReason);
            }
            else
            {
                personnelFile.ClearRehireBlock();
            }

            // [3] Reopen EXACTLY the rows the execution closed, restoring each one's previous end date
            // (null when the execution set it — no invented dates, D-11). No rehire happened (guard above),
            // so no newer rows exist and the single-active-primary rule is restored as it was.
            var assignmentIds = entity.ClosedRecords
                .Where(record => record.EntityKind == RetirementClosedRecordKinds.Assignment)
                .ToDictionary(record => record.EntityPublicId, record => record.PreviousEndDate);
            var contractIds = entity.ClosedRecords
                .Where(record => record.EntityKind == RetirementClosedRecordKinds.Contract)
                .ToDictionary(record => record.EntityPublicId, record => record.PreviousEndDate);

            var assignments = await employeeRepository.GetEmploymentAssignmentsByPublicIdsAsync(tenantId, assignmentIds.Keys.ToArray(), cancellationToken);
            foreach (var assignment in assignments)
            {
                assignment.Reopen(assignmentIds[assignment.PublicId]);
            }

            var contracts = await employeeRepository.GetContractHistoriesByPublicIdsAsync(tenantId, contractIds.Keys.ToArray(), cancellationToken);
            foreach (var contract in contracts)
            {
                contract.Reopen(contractIds[contract.PublicId]);
            }

            // [4] Login: reactivate ONLY if it was active before the execution (E-16 — a login deactivated
            // manually before the baja stays deactivated; the snapshot rules, not an assumption).
            if (entity.PriorLoginWasActive == true && personnelFile.LinkedUserPublicId is { } loginToReactivate)
            {
                _ = await companyUserLifecycleService.ReactivateCoreAsync(tenantId, loginToReactivate, cancellationToken);
            }

            // [5] Exit interview: the baja "did not happen" — archive the submissions so they never count in
            // rotation analytics (D-09; same bulk mechanism as the rehire; there is no un-archive).
            _ = await exitInterviewRepository.ArchiveSubmissionsForFileAsync(tenantId, personnelFile.Id, cancellationToken);

            // [6] Journal the REVERSION_BAJA action (D-15).
            var action = PersonnelFilePersonnelAction.Create(
                ReversalActionTypeCode,
                AppliedActionStatusCode,
                actionDateUtc: nowUtc,
                effectiveFromUtc: nowUtc,
                effectiveToUtc: null,
                description: $"Reversión del retiro definitivo efectivo {entity.RetirementDate:yyyy-MM-dd}. Motivo: {command.Reason.Trim()}",
                reference: null,
                amount: null,
                currencyCode: null,
                isSystemGenerated: true);
            action.BindToPersonnelFile(personnelFile.Id);
            action.SetTenantId(tenantId);
            _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);

            // [7] The request keeps its full history and moves to REVERTIDA (RN-010.4); the employee becomes
            // eligible for a NEW request (RN-010.5) and seniority resumes continuous (RN-13).
            entity.MarkReverted(revertedByUserId, nowUtc, command.Reason);
            TouchPersonnelFile(personnelFile);

            var response = RetirementRequestMapping.ToResponse(entity);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService,
                personnelFile,
                $"Reverted retirement for {personnelFile.FullName} (was effective {entity.RetirementDate:yyyy-MM-dd}).",
                new
                {
                    RestoredEmploymentStatusCode = entity.PriorEmploymentStatusCode,
                    LoginReactivated = entity.PriorLoginWasActive == true,
                    RehireBlockRestored = entity.PriorRehireBlocked,
                    ReopenedAssignments = assignments.Count,
                    ReopenedContracts = contracts.Count
                },
                response,
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileRetirementRequestResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
