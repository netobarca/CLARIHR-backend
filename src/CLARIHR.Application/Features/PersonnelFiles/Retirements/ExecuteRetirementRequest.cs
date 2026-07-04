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
/// Orchestrated execution of an AUTORIZADA retirement (RF-006, D-05/D-06/D-18): a single transaction that
/// stamps the profile (+RETIRADO), deactivates the file (optionally blocking rehire), closes the active
/// plazas/contracts at the retirement date CAPTURING the reversal snapshot (D-11), deactivates the linked
/// login, journals the BAJA personnel action (D-15) and marks the request EJECUTADA.
/// </summary>
public sealed record ExecuteRetirementRequestCommand(
    Guid PersonnelFileId,
    Guid RetirementRequestPublicId,
    bool BlockRehire,
    string? RehireBlockReason,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileRetirementRequestResponse>;

internal sealed class ExecuteRetirementRequestCommandValidator : AbstractValidator<ExecuteRetirementRequestCommand>
{
    public ExecuteRetirementRequestCommandValidator()
    {
        RuleFor(c => c.PersonnelFileId).NotEmpty();
        RuleFor(c => c.RetirementRequestPublicId).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.RehireBlockReason).MaximumLength(500);
    }
}

internal sealed class ExecuteRetirementRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompanyUserLifecycleService companyUserLifecycleService,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ExecuteRetirementRequestCommand, PersonnelFileRetirementRequestResponse>
{
    private const string RetirementActionTypeCode = "BAJA";
    private const string AppliedActionStatusCode = "APLICADA";
    private const string LoginRevocationReason = "personnel-file-retired";

    public async Task<Result<PersonnelFileRetirementRequestResponse>> Handle(
        ExecuteRetirementRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRetirementsAsync<PersonnelFileRetirementRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var tenantId = personnelFile!.TenantId;
        var entity = await employeeRepository.GetRetirementRequestEntityAsync(
            personnelFile.PublicId, command.RetirementRequestPublicId, tenantId, includeClosedRecords: false, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.RequestStatusCode != RetirementRequestStatuses.Autorizada)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.StateRuleViolation);
        }

        // D-05: manual execution, only once the retirement date arrives (UTC-date semantics).
        var nowUtc = dateTimeProvider.UtcNow;
        if (!RetirementRequestRules.IsExecutableOn(entity.RetirementDate, nowUtc))
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.ExecutionDateNotReached);
        }

        // D-13: the subject employee never executes their own baja.
        _ = Guid.TryParse(currentUserService.UserId, out var executedByUserId);
        if (personnelFile.LinkedUserPublicId is { } subjectUserId && subjectUserId == executedByUserId)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.SelfActionForbidden);
        }

        // E-07: the profile must still be in an executable state (not retired/removed by another operation).
        var profile = await employeeRepository.GetEmployeeProfileEntityAsync(personnelFile.Id, tenantId, cancellationToken);
        if (profile is null || profile.RetirementDate is not null || !personnelFile.IsActive)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.ExecutionStateConflict);
        }

        // RF-016: a hire date moved after the authorization must not produce retirement < hire.
        if (entity.RetirementDate.Date < profile.HireDate.Date)
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.DateIncoherent);
        }

        // R-T5: closing at the retirement date would violate the end-after-start check constraints when an
        // active row STARTS after that date (e.g. a plaza granted after a retroactive baja was authorized).
        var activeRowStartDates = await employeeRepository.GetActiveRowStartDatesAsync(personnelFile.Id, tenantId, cancellationToken);
        if (RetirementRequestRules.HasClosingBlockers(activeRowStartDates, entity.RetirementDate))
        {
            return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.DateIncoherent);
        }

        // Reversal snapshot pre-reads (D-11) + last-admin invariant (pre-development clarification #4).
        var priorEmploymentStatusCode = profile.EmploymentStatusCode;
        var priorRehireBlocked = personnelFile.IsRehireBlocked;
        var priorRehireBlockReason = personnelFile.RehireBlockedReason;
        bool? priorLoginWasActive = null;
        if (personnelFile.LinkedUserPublicId is { } linkedUserPublicId)
        {
            priorLoginWasActive = await companyUserLifecycleService.GetLoginIsActiveAsync(linkedUserPublicId, cancellationToken);
            if (priorLoginWasActive == true
                && await companyUserLifecycleService.IsLastActiveAdministratorAsync(tenantId, linkedUserPublicId, cancellationToken))
            {
                return Result<PersonnelFileRetirementRequestResponse>.Failure(RetirementErrors.LastAdminConflict);
            }
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // [1] Profile: retirement metadata + RETIRADO (the ONLY writer after D-01).
            profile.ApplyRetirement(entity.RetirementCategoryCode, entity.RetirementReasonCode, entity.Notes, entity.RetirementDate);

            // [2] File: inactive; optional rehire block in the same operation (D-18).
            personnelFile.Inactivate();
            if (command.BlockRehire)
            {
                personnelFile.BlockRehire(command.RehireBlockReason);
            }

            // [3] Close plazas + contracts at the retirement date, CAPTURING the reversal snapshot (D-06/D-11).
            var assignmentCaptures = await employeeRepository.CloseActiveEmploymentAssignmentsCapturingAsync(
                personnelFile.Id, tenantId, entity.RetirementDate, cancellationToken);
            foreach (var capture in assignmentCaptures)
            {
                var record = RetirementRequestClosedRecord.Create(
                    RetirementClosedRecordKinds.Assignment, capture.EntityPublicId, capture.PreviousEndDate);
                record.SetTenantId(tenantId);
                entity.AddClosedRecord(record);
            }

            var contractCaptures = await employeeRepository.CloseActiveContractHistoriesCapturingAsync(
                personnelFile.Id, tenantId, entity.RetirementDate, cancellationToken);
            foreach (var capture in contractCaptures)
            {
                var record = RetirementRequestClosedRecord.Create(
                    RetirementClosedRecordKinds.Contract, capture.EntityPublicId, capture.PreviousEndDate);
                record.SetTenantId(tenantId);
                entity.AddClosedRecord(record);
            }

            // [4] Deactivate the linked login (D-06). The link itself is NOT cleared — the reversal restores
            // the login, and only a rehire re-links (uq_personnel_files__tenant_linked_user stays satisfied).
            if (personnelFile.LinkedUserPublicId is { } loginToDeactivate)
            {
                _ = await companyUserLifecycleService.DeactivateCoreAsync(tenantId, loginToDeactivate, LoginRevocationReason, cancellationToken);
            }

            // [5] Journal the BAJA personnel action (D-15) with the seeded APLICADA status.
            var action = PersonnelFilePersonnelAction.Create(
                RetirementActionTypeCode,
                AppliedActionStatusCode,
                actionDateUtc: nowUtc,
                effectiveFromUtc: entity.RetirementDate,
                effectiveToUtc: null,
                description: $"Retiro definitivo ({entity.RetirementCategoryCode}/{entity.RetirementReasonCode}) efectivo {entity.RetirementDate:yyyy-MM-dd}.",
                reference: null,
                amount: null,
                currencyCode: null,
                isSystemGenerated: true);
            action.BindToPersonnelFile(personnelFile.Id);
            action.SetTenantId(tenantId);
            _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);

            // [6] Mark EJECUTADA with the exact execution timestamp (30-day reversal window anchor) + snapshot.
            entity.MarkExecuted(executedByUserId, nowUtc, priorEmploymentStatusCode, priorLoginWasActive, priorRehireBlocked, priorRehireBlockReason);
            TouchPersonnelFile(personnelFile);

            var response = RetirementRequestMapping.ToResponse(entity);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService,
                personnelFile,
                $"Executed retirement for {personnelFile.FullName} (effective {entity.RetirementDate:yyyy-MM-dd}).",
                new
                {
                    PriorEmploymentStatusCode = priorEmploymentStatusCode,
                    PriorLoginWasActive = priorLoginWasActive,
                    PriorRehireBlocked = priorRehireBlocked,
                    ClosedAssignments = assignmentCaptures.Count,
                    ClosedContracts = contractCaptures.Count
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
