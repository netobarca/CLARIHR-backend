using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

public sealed record FinalizePersonnelFileResponse(
    PersonnelFileResponse PersonnelFile,
    CompanyUserResponse User,
    DateTime? InvitationExpiresUtc);

public sealed record FinalizePersonnelFileCommand(
    Guid PersonnelFileId,
    Guid ConcurrencyToken) : ICommand<FinalizePersonnelFileResponse>;

internal sealed class FinalizePersonnelFileCommandValidator : AbstractValidator<FinalizePersonnelFileCommand>
{
    public FinalizePersonnelFileCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class FinalizePersonnelFileCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPositionSlotRepository positionSlotRepository,
    ICompanyUserProvisioningService companyUserProvisioningService,
    IUserRepository userRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<FinalizePersonnelFileCommand, FinalizePersonnelFileResponse>
{
    public async Task<Result<FinalizePersonnelFileResponse>> Handle(
        FinalizePersonnelFileCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var tenantId = tenantContext.TenantId.Value;
        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await personnelFileRepository.GetByIdAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(
                await personnelFileRepository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        if (personnelFile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (personnelFile.RecordType != CLARIHR.Domain.PersonnelFiles.PersonnelFileRecordType.Employee)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(PersonnelFileErrors.FinalizeOnlyEmployee);
        }

        if (personnelFile.LifecycleStatus != CLARIHR.Domain.PersonnelFiles.PersonnelFileLifecycleStatus.Draft ||
            personnelFile.LinkedUserPublicId.HasValue)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (string.IsNullOrWhiteSpace(personnelFile.InstitutionalEmail))
        {
            return Result<FinalizePersonnelFileResponse>.Failure(PersonnelFileErrors.FinalizeRequiresInstitutionalEmail);
        }

        if (!personnelFile.AssignedPositionSlotPublicId.HasValue)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(PersonnelFileErrors.FinalizeRequiresAssignedPositionSlot);
        }

        var slot = await positionSlotRepository.GetByIdAsync(personnelFile.AssignedPositionSlotPublicId.Value, cancellationToken);
        if (slot is null)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(
                await positionSlotRepository.ExistsOutsideTenantAsync(personnelFile.AssignedPositionSlotPublicId.Value, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionSlotErrors.PositionSlotNotFound);
        }

        var slotResponse = await positionSlotRepository.GetResponseByIdAsync(slot.PublicId, cancellationToken);
        if (slotResponse?.RoleId is null)
        {
            return Result<FinalizePersonnelFileResponse>.Failure(PersonnelFileErrors.FinalizeRequiresPositionSlotRole);
        }

        var existingUser = await userRepository.GetByEmailAsync(personnelFile.InstitutionalEmail, cancellationToken);
        if (existingUser is not null)
        {
            var linkedPersonnelFile = await personnelFileRepository.GetByLinkedUserIdAsync(tenantId, existingUser.PublicId, cancellationToken);
            if (linkedPersonnelFile is not null && linkedPersonnelFile.PublicId != personnelFile.PublicId)
            {
                return Result<FinalizePersonnelFileResponse>.Failure(PersonnelFileErrors.LinkedUserConflict);
            }
        }

        var before = await personnelFileRepository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file response could not be resolved before finalization.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var provisioningResult = await companyUserProvisioningService.ProvisionAsync(
                new CompanyUserProvisioningRequest(
                    tenantId,
                    personnelFile.InstitutionalEmail!,
                    personnelFile.FirstName,
                    personnelFile.LastName,
                    slotResponse.RoleId.Value,
                    Country: null,
                    Source: "personnel-file-finalization",
                    AllowExistingMembershipReuse: true),
                cancellationToken);
            if (provisioningResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<FinalizePersonnelFileResponse>.Failure(provisioningResult.Error);
            }

            personnelFile.Complete(provisioningResult.Value.User.PublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await personnelFileRepository.GetResponseByIdAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file response could not be resolved after finalization.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PersonnelFileCompleted,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    $"Completed personnel file {personnelFile.FullName}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<FinalizePersonnelFileResponse>.Success(
                new FinalizePersonnelFileResponse(
                    after,
                    provisioningResult.Value.UserResponse,
                    provisioningResult.Value.InvitationExpiresUtc));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
