using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record RehireEmployeeResponse(
    PersonnelFileResponse PersonnelFile,
    CompanyUserResponse? User,
    DateTime? InvitationExpiresUtc);

/// <summary>
/// Atomic orchestrator for the employee-rehire flow (§3.3). Reuses the existing file (D-01): it
/// validates eligibility (RF-002), closes the prior period as derived history (RF-004/D-14),
/// reopens the file (D-08), opens a new period (new hire date, contract, multi-plaza assignment —
/// RF-003/RF-006), re-provisions the user account (RF-008/D-09) and records the RECONTRATACION
/// personnel action (RF-009) — all in one transaction (RN-11/E9).
/// </summary>
public sealed record RehireEmployeeCommand(
    Guid PersonnelFileId,
    Guid ConcurrencyToken,
    DateTime NewHireDate,
    string ContractTypeCode,
    DateTime ContractStartDate,
    DateTime? ContractEndDate,
    Guid PositionSlotPublicId,
    string AssignmentTypeCode,
    bool CreateUserAccount,
    string? NewInstitutionalEmail,
    bool PriorPeriodClosureConfirmed,
    string? AuthorizationReason) : ICommand<RehireEmployeeResponse>;

internal sealed class RehireEmployeeCommandValidator : AbstractValidator<RehireEmployeeCommand>
{
    public RehireEmployeeCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.NewHireDate).NotEmpty();
        RuleFor(command => command.ContractTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ContractStartDate).NotEmpty();
        RuleFor(command => command.ContractEndDate)
            .GreaterThan(command => command.ContractStartDate)
            .When(command => command.ContractEndDate.HasValue);
        RuleFor(command => command.PositionSlotPublicId).NotEmpty();
        RuleFor(command => command.AssignmentTypeCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.NewInstitutionalEmail)
            .EmailAddress()
            .When(command => !string.IsNullOrWhiteSpace(command.NewInstitutionalEmail));
        RuleFor(command => command.AuthorizationReason).MaximumLength(500);
    }
}

internal sealed class RehireEmployeeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IExitInterviewRepository exitInterviewRepository,
    IPositionSlotRepository positionSlotRepository,
    IPersonnelFileFinalizationService finalizationService,
    IUserRepository userRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RehireEmployeeCommand, RehireEmployeeResponse>
{
    private const string EmploymentStatusActive = "ACTIVO";
    private const string RehireActionTypeCode = "RECONTRATACION";
    // APLICADA is the seeded ActionStatus for system-applied actions; the previous "COMPLETADA" was
    // never seeded in the catalog (D-15 of the retirement module fixes the orphan code + data).
    private const string RehireActionStatusCode = "APLICADA";

    public async Task<Result<RehireEmployeeResponse>> Handle(
        RehireEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<RehireEmployeeResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RehireEmployeeResponse>.Failure(authorizationResult.Error);
        }

        // [1] Load the tracked file (mirrors Finalize): GetByIdAsync returns a tracked entity so the
        // domain mutations below (ReopenForRehire / Complete / SetInstitutionalEmail) persist.
        var personnelFile = await personnelFileRepository.GetByIdAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<RehireEmployeeResponse>.Failure(
                await personnelFileRepository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RehireEmployeeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // The 1:1 employee profile carries the RetirementDate the eligibility rule reads as the "retired"
        // signal, and the EmployeeCode the new period preserves (D-03).
        var existingProfile = await employeeRepository.GetEmployeeProfileAsync(personnelFile.PublicId, cancellationToken);
        if (existingProfile is null)
        {
            return Result<RehireEmployeeResponse>.Failure(RehireErrors.NotRetired);
        }

        // [2] RF-002 — eligibility + authorized override of a "not rehireable" file (D-04/D-11).
        var callerCanAuthorize = await authorizationService.HasRehireAuthorizationAsync(tenantId, cancellationToken);
        var eligibility = RehireEligibilityRules.Evaluate(new RehireEligibilityRules.Input(
            personnelFile.RecordType,
            personnelFile.LifecycleStatus,
            IsRetired: existingProfile.RetirementDate is not null,
            personnelFile.IsRehireBlocked,
            callerCanAuthorize,
            AuthorizationReasonProvided: !string.IsNullOrWhiteSpace(command.AuthorizationReason),
            command.PriorPeriodClosureConfirmed));
        if (eligibility.IsFailure)
        {
            return Result<RehireEmployeeResponse>.Failure(eligibility.Error);
        }

        // Validate the new assignment type against the catalog before any mutation (RF-006).
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
            tenantId, PersonnelCurriculumCatalogCategories.AssignmentType, command.AssignmentTypeCode, cancellationToken))
        {
            return Result<RehireEmployeeResponse>.Failure(EmploymentAssignmentErrors.TypeCodeInvalid);
        }

        var before = await personnelFileRepository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file response could not be resolved before rehire.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // [3] RF-004/D-14 — close the prior period so it survives as derived history and stops
            // consuming slot capacity. Boundary-stamped at the new hire date.
            await employeeRepository.CloseActiveContractHistoriesAsync(personnelFile.Id, tenantId, command.NewHireDate, cancellationToken);
            await employeeRepository.CloseActiveEmploymentAssignmentsAsync(personnelFile.Id, tenantId, command.NewHireDate, cancellationToken);

            // [4] D-08 — reopen to Draft, clear the linked user, reactivate the file.
            personnelFile.ReopenForRehire();

            // [7] D-09 — adopt a new institutional email only if one was supplied (previous in use).
            if (!string.IsNullOrWhiteSpace(command.NewInstitutionalEmail))
            {
                personnelFile.SetInstitutionalEmail(command.NewInstitutionalEmail);
            }

            // [5] Upsert the 1:1 profile for the new period: new hire date (resets antigüedad, D-03),
            // active status, EmployeeCode preserved. Contract data and the structural units now live
            // per-plaza on the assignment, not the profile.
            var profile = PersonnelFileEmployeeProfile.Create(
                existingProfile.EmployeeCode,
                EmploymentStatusActive,
                command.NewHireDate);
            profile.BindToPersonnelFile(personnelFile.Id);
            profile.SetTenantId(tenantId);
            _ = await employeeRepository.UpsertEmployeeProfileAsync(profile, cancellationToken);

            // The upsert deliberately no longer touches the retirement metadata (retirement module D-01),
            // so the rehire clears the closed period's baja explicitly on the tracked row.
            var trackedProfile = await employeeRepository.GetEmployeeProfileEntityAsync(personnelFile.Id, tenantId, cancellationToken);
            trackedProfile?.ClearRetirement(EmploymentStatusActive);

            // Flush so the new-assignment capacity/overlap query below observes the closed prior
            // period (same connection/transaction) instead of the still-active rows.
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            // D-12 — archive the prior period's exit-interview submissions: they belong to the closed baja
            // and must not surface as active for the reopened (new) period.
            _ = await exitInterviewRepository.ArchiveSubmissionsForFileAsync(tenantId, personnelFile.Id, cancellationToken);

            // [6] New contract for the new period.
            var contract = PersonnelFileContractHistory.Create(
                command.ContractTypeCode,
                command.ContractStartDate,
                command.ContractEndDate,
                command.PositionSlotPublicId,
                isActive: true,
                notes: null);
            contract.BindToPersonnelFile(personnelFile.Id);
            contract.SetTenantId(tenantId);
            _ = await employeeRepository.AddContractHistoryAsync(personnelFile.Id, tenantId, contract, cancellationToken);

            // [6] New employment assignment, reusing the multi-plaza rules (RN-08/D-16).
            var assignmentError = await AddRehireAssignmentAsync(personnelFile, tenantId, command, cancellationToken);
            if (assignmentError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RehireEmployeeResponse>.Failure(assignmentError);
            }

            // [8] Re-validate as a finalize: resolves the slot role and detects an email conflict
            // (D-09) using the existing resolver, now that the file is a Draft with no linked user.
            var validation = await FinalizePersonnelFileValidationResolver.ValidateAsync(
                tenantId,
                personnelFile,
                command.PositionSlotPublicId,
                command.CreateUserAccount,
                includeRelatedResourceTenantMismatch: true,
                authorizationService,
                positionSlotRepository,
                personnelFileRepository,
                employeeRepository,
                userRepository,
                cancellationToken);
            if (!validation.IsEligible)
            {
                await transaction.RollbackAsync(cancellationToken);
                // E6 — surface "email already taken" as a rehire-specific, actionable code (D-09).
                return Result<RehireEmployeeResponse>.Failure(
                    validation.PrimaryError == PersonnelFileErrors.LinkedUserConflict
                        ? RehireErrors.InstitutionalEmailInUse
                        : validation.PrimaryError);
            }

            // [8] Re-provision / re-link via the shared finalization core (reuses the prior account
            // when its email is still free — D-09), transitioning the file back to Completed.
            var finalization = await finalizationService.ApplyAsync(
                tenantId,
                personnelFile,
                command.CreateUserAccount,
                validation.ResolvedRoleId,
                source: "personnel-file-rehire",
                cancellationToken);
            if (finalization.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RehireEmployeeResponse>.Failure(finalization.Error);
            }

            // [9] RF-009/RN-10 — append-only RECONTRATACION action (override note carried if any).
            var action = PersonnelFilePersonnelAction.Create(
                RehireActionTypeCode,
                RehireActionStatusCode,
                actionDateUtc: command.NewHireDate,
                effectiveFromUtc: command.NewHireDate,
                effectiveToUtc: null,
                description: BuildActionDescription(command),
                reference: null,
                amount: null,
                currencyCode: null,
                isSystemGenerated: true);
            action.BindToPersonnelFile(personnelFile.Id);
            action.SetTenantId(tenantId);
            _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await personnelFileRepository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file response could not be resolved after rehire.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileCompleted,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Rehired employee {personnelFile.FullName} effective {command.NewHireDate:yyyy-MM-dd}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<RehireEmployeeResponse>.Success(
                new RehireEmployeeResponse(after, finalization.Value.User, finalization.Value.InvitationExpiresUtc));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Builds the new period's employment assignment through the same multi-plaza rules the
    /// dedicated endpoint uses. Returns the rule error, or null on success.
    /// </summary>
    private async Task<Error?> AddRehireAssignmentAsync(
        PersonnelFile personnelFile,
        Guid tenantId,
        RehireEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        var existing = (await employeeRepository.GetEmploymentAssignmentsAsync(personnelFile.PublicId, cancellationToken))
            .Select(assignment => new EmploymentAssignmentRules.ExistingAssignment(
                assignment.Id,
                assignment.PositionSlotId,
                assignment.StartDate,
                assignment.EndDate,
                assignment.IsPrimary,
                assignment.IsActive))
            .ToArray();

        var slotFacts = await EmploymentAssignmentSlotResolver.ResolveAsync(
            positionSlotRepository,
            employeeRepository,
            tenantId,
            command.PositionSlotPublicId,
            command.NewHireDate,
            command.ContractEndDate,
            isActive: true,
            excludeAssignmentPublicId: null,
            cancellationToken);

        // The new period's plaza is the (only) active assignment, so it is the primary (RN-03).
        var candidate = new EmploymentAssignmentRules.Candidate(
            PublicId: null,
            command.PositionSlotPublicId,
            command.NewHireDate,
            command.ContractEndDate,
            IsPrimary: true,
            IsActive: true);

        var evaluation = EmploymentAssignmentRules.Evaluate(candidate, existing, slotFacts);
        if (evaluation.IsFailure)
        {
            return evaluation.Error;
        }

        await employeeRepository.DemoteEmploymentAssignmentsAsync(tenantId, evaluation.Value.PrimariesToDemote, cancellationToken);

        var entity = PersonnelFileEmploymentAssignment.Create(
            command.AssignmentTypeCode,
            command.ContractTypeCode,
            workdayCode: null,
            payrollTypeCode: null,
            command.PositionSlotPublicId,
            orgUnitPublicId: null,
            workCenterPublicId: null,
            costCenterPublicId: null,
            command.NewHireDate,
            command.ContractEndDate,
            isPrimary: true,
            isActive: true,
            notes: null);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(tenantId);
        _ = await employeeRepository.AddEmploymentAssignmentAsync(personnelFile.Id, tenantId, entity, cancellationToken);

        return null;
    }

    private static string BuildActionDescription(RehireEmployeeCommand command)
    {
        var description = $"Rehire effective {command.NewHireDate:yyyy-MM-dd} on position slot {command.PositionSlotPublicId}.";
        if (!string.IsNullOrWhiteSpace(command.AuthorizationReason))
        {
            description += $" Authorized override: {command.AuthorizationReason}";
        }

        return description;
    }
}
