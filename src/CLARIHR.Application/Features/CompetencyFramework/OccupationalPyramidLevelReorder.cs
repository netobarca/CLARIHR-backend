using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Common;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using FluentValidation;

using CLARIHR.Application.Common.Policies;

namespace CLARIHR.Application.Features.CompetencyFramework;

/// <summary>
/// H-11 — one level of the reordered pyramid, as echoed back so the client can confirm what was applied without
/// a second read.
/// </summary>
public sealed record OccupationalPyramidLevelOrderItemResponse(
    Guid Id,
    string Code,
    int LevelOrder);

public sealed record ReorderOccupationalPyramidLevelsResponse(
    IReadOnlyList<OccupationalPyramidLevelOrderItemResponse> Levels,
    // H-11/H-33 — el marcador que el guardrail `OptedInControllers_ReturnMarkerTypeOnEveryPutPatchAction`
    // exige a toda acción PUT/PATCH de un controlador con `[ResourceActions]`. Faltaba desde H-11, y el
    // guardrail llevaba rojo desde entonces sin que nadie lo viera: la suite completa no se corría.
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

/// <summary>
/// H-11 — the client sends the COMPLETE set of level ids in the desired order; the server assigns the ranks.
/// No numbers travel in the request, so a client cannot construct a collision with the unique rank index.
/// <para>
/// Deliberately NO <c>If-Match</c>: there is no single aggregate to hold a token for, and the request carries
/// the whole desired order, which makes last-writer-wins the honest semantics of a drag-and-drop save.
/// </para>
/// </summary>
public sealed record ReorderOccupationalPyramidLevelsCommand(
    Guid CompanyId,
    IReadOnlyList<Guid> OrderedPublicIds) : ICommand<ReorderOccupationalPyramidLevelsResponse>;

internal sealed class ReorderOccupationalPyramidLevelsCommandValidator
    : AbstractValidator<ReorderOccupationalPyramidLevelsCommand>
{
    public ReorderOccupationalPyramidLevelsCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.OrderedPublicIds).NotEmpty();
        RuleForEach(command => command.OrderedPublicIds).NotEmpty();
    }
}

internal sealed class ReorderOccupationalPyramidLevelsCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReorderOccupationalPyramidLevelsCommand, ReorderOccupationalPyramidLevelsResponse>
{
    public async Task<Result<ReorderOccupationalPyramidLevelsResponse>> Handle(
        ReorderOccupationalPyramidLevelsCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ReorderOccupationalPyramidLevelsResponse>.Failure(authorizationResult.Error);
        }

        var levels = await repository.GetAllOccupationalPyramidLevelsAsync(command.CompanyId, cancellationToken);
        if (levels.Count == 0)
        {
            return Result<ReorderOccupationalPyramidLevelsResponse>.Failure(
                CompetencyFrameworkErrors.OccupationalPyramidLevelOrderSetIncomplete);
        }

        var permutationResult = CatalogBulkReorder.EnsureIsCompletePermutation(
            command.OrderedPublicIds,
            levels.Select(level => level.PublicId).ToArray(),
            CompetencyFrameworkErrors.OccupationalPyramidLevelOrderSetIncomplete);
        if (permutationResult.IsFailure)
        {
            return Result<ReorderOccupationalPyramidLevelsResponse>.Failure(permutationResult.Error);
        }

        var byPublicId = levels.ToDictionary(level => level.PublicId);
        var ordered = command.OrderedPublicIds.Select(id => byPublicId[id]).ToArray();
        var before = levels
            .Select(level => new OccupationalPyramidLevelOrderItemResponse(level.PublicId, level.Code, level.LevelOrder))
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // ── Phase 1: park the whole set above every number in play ──
            // The rank is UNIQUE per tenant and the index is not deferrable, so Postgres checks it per UPDATE.
            // Writing the final ranks in one pass would break on the first row whose new rank still belongs to
            // another row — a plain swap being the smallest case. Nothing here can collide: the staging band
            // starts strictly above both the current maximum and the highest rank about to be assigned.
            var bandStart = CatalogBulkReorder.ResolveStagingBandStart(
                levels.Max(level => level.LevelOrder), ordered.Length);
            for (var index = 0; index < ordered.Length; index++)
            {
                ordered[index].SetLevelOrder(bandStart + index);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            // ── Phase 2: the real ranks, now that the band is empty ──
            for (var index = 0; index < ordered.Length; index++)
            {
                ordered[index].SetLevelOrder(CatalogBulkReorder.OrderAt(index));
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = ordered
                .Select(level => new OccupationalPyramidLevelOrderItemResponse(level.PublicId, level.Code, level.LevelOrder))
                .ToArray();

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OccupationalPyramidLevelUpdated,
                    AuditEntityTypes.OccupationalPyramidLevel,
                    command.CompanyId,
                    $"{ordered.Length} levels",
                    AuditActions.Update,
                    $"Reordered {ordered.Length} occupational pyramid level(s).",
                    Before: new ReorderOccupationalPyramidLevelsResponse(before),
                    After: new ReorderOccupationalPyramidLevelsResponse(after)),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<ReorderOccupationalPyramidLevelsResponse>.Success(
                new ReorderOccupationalPyramidLevelsResponse(after));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
