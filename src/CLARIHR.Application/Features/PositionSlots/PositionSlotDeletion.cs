using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.PositionSlots;

/// <summary>
/// H-15 — a slot created by mistake used to be permanent: the controller had no `DELETE`, no retire route, and
/// no writable `isActive`, so removing the two throwaway slots of the API run required raw SQL.
/// <para>
/// This deletes for real, and it is safe only because of the usage guard. Nothing has a foreign key to
/// <c>position_slots</c> except its own self-references, so the database would happily delete a slot that
/// employment history still points at — by <c>public_id</c>, from four different tables.
/// </para>
/// </summary>
/// <remarks>
/// Returns the deleted snapshot rather than an empty `204`, matching the DELETE of `job-catalogs` and of the
/// job profile sub-resources. Inventing a third shape for the same verb is precisely what H-11 was about.
/// </remarks>
public sealed record DeletePositionSlotCommand(
    Guid PositionSlotId,
    Guid ConcurrencyToken) : ICommand<PositionSlotResponse>;

internal sealed class DeletePositionSlotCommandValidator : AbstractValidator<DeletePositionSlotCommand>
{
    public DeletePositionSlotCommandValidator()
    {
        RuleFor(command => command.PositionSlotId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class DeletePositionSlotCommandHandler(
    IPositionSlotAuthorizationService authorizationService,
    IPositionSlotRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeletePositionSlotCommand, PositionSlotResponse>
{
    public async Task<Result<PositionSlotResponse>> Handle(DeletePositionSlotCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionSlotResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(authorizationResult.Error);
        }

        var slot = await repository.GetByIdAsync(command.PositionSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<PositionSlotResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PositionSlotId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Delete)
                    : PositionSlotErrors.PositionSlotNotFound);
        }

        if (slot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.ConcurrencyConflict);
        }

        var usage = await repository.GetUsageAsync(slot.PublicId, slot.Id, cancellationToken);
        if (usage.BlocksDeletion)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.InUse);
        }

        // Snapshot before the row is gone: after the delete there is nothing left to describe in the audit
        // trail, and this is the one operation with no "after" state to fall back on.
        var before = await repository.GetResponseByIdAsync(slot.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Position slot response could not be resolved before deletion.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Remove(slot);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionSlotDeleted,
                    AuditEntityTypes.PositionSlot,
                    slot.PublicId,
                    slot.Code,
                    AuditActions.Delete,
                    $"Deleted position slot {slot.Code}.",
                    Before: before),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionSlotResponse>.Success(before);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
