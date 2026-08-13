using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using FluentValidation;

using CLARIHR.Application.Common.Policies;

namespace CLARIHR.Application.Features.PositionDescriptionCatalogs;

public sealed record PositionDescriptionCatalogItemOrderItemResponse(
    Guid Id,
    string Code,
    int SortOrder);

public sealed record ReorderPositionDescriptionCatalogItemsResponse(
    IReadOnlyList<PositionDescriptionCatalogItemOrderItemResponse> Items,
    // H-11/H-33 — el marcador que el guardrail `OptedInControllers_ReturnMarkerTypeOnEveryPutPatchAction`
    // exige a toda acción PUT/PATCH de un controlador con `[ResourceActions]`. Faltaba desde H-11, y el
    // guardrail llevaba rojo desde entonces sin que nadie lo viera: la suite completa no se corría.
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

/// <summary>
/// H-11 — bulk reorder of one catalog type. Same contract as the occupational pyramid's (complete set of ids in
/// the desired order, server assigns 10, 20, 30…) but a single write phase: this order is NOT unique, so no
/// intermediate state can violate anything.
/// </summary>
public sealed record ReorderPositionDescriptionCatalogItemsCommand(
    Guid CompanyId,
    PositionDescriptionCatalogType CatalogType,
    IReadOnlyList<Guid> OrderedPublicIds) : ICommand<ReorderPositionDescriptionCatalogItemsResponse>;

internal sealed class ReorderPositionDescriptionCatalogItemsCommandValidator
    : AbstractValidator<ReorderPositionDescriptionCatalogItemsCommand>
{
    public ReorderPositionDescriptionCatalogItemsCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.OrderedPublicIds).NotEmpty();
        RuleForEach(command => command.OrderedPublicIds).NotEmpty();

        // The catalog-type check lives in the handler, not here: it already has a catalogued and localised code
        // (`POSITION_DESCRIPTION_CATALOG_INVALID_TYPE`). Expressing it as a validator message would have minted
        // a second, untranslated way of saying the same thing — which the localisation guardrail caught.
    }
}

internal sealed class ReorderPositionDescriptionCatalogItemsCommandHandler(
    IPositionDescriptionCatalogAuthorizationService authorizationService,
    IPositionDescriptionCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReorderPositionDescriptionCatalogItemsCommand, ReorderPositionDescriptionCatalogItemsResponse>
{
    public async Task<Result<ReorderPositionDescriptionCatalogItemsResponse>> Handle(
        ReorderPositionDescriptionCatalogItemsCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ReorderPositionDescriptionCatalogItemsResponse>.Failure(authorizationResult.Error);
        }

        if (!PositionDescriptionCatalogErrors.IsSimpleCatalogType(command.CatalogType))
        {
            return Result<ReorderPositionDescriptionCatalogItemsResponse>.Failure(
                PositionDescriptionCatalogErrors.InvalidCatalogType);
        }

        var items = await repository.GetAllCatalogItemsAsync(command.CompanyId, command.CatalogType, cancellationToken);
        if (items.Count == 0)
        {
            return Result<ReorderPositionDescriptionCatalogItemsResponse>.Failure(
                PositionDescriptionCatalogErrors.CatalogOrderSetIncomplete);
        }

        var permutationResult = CatalogBulkReorder.EnsureIsCompletePermutation(
            command.OrderedPublicIds,
            items.Select(item => item.PublicId).ToArray(),
            PositionDescriptionCatalogErrors.CatalogOrderSetIncomplete);
        if (permutationResult.IsFailure)
        {
            return Result<ReorderPositionDescriptionCatalogItemsResponse>.Failure(permutationResult.Error);
        }

        var byPublicId = items.ToDictionary(item => item.PublicId);
        var ordered = command.OrderedPublicIds.Select(id => byPublicId[id]).ToArray();
        var before = items
            .Select(item => new PositionDescriptionCatalogItemOrderItemResponse(item.PublicId, item.Code, item.SortOrder))
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            for (var index = 0; index < ordered.Length; index++)
            {
                ordered[index].SetSortOrder(CatalogBulkReorder.OrderAt(index));
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            PositionDescriptionCatalogCacheInvalidation.InvalidateSimple(repository, command.CompanyId, command.CatalogType);

            var after = ordered
                .Select(item => new PositionDescriptionCatalogItemOrderItemResponse(item.PublicId, item.Code, item.SortOrder))
                .ToArray();

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionDescriptionCatalogItemUpdated,
                    AuditEntityTypes.PositionDescriptionCatalogItem,
                    command.CompanyId,
                    $"{command.CatalogType}: {ordered.Length} items",
                    AuditActions.Update,
                    $"Reordered {ordered.Length} {command.CatalogType} catalog item(s).",
                    Before: new ReorderPositionDescriptionCatalogItemsResponse(before),
                    After: new ReorderPositionDescriptionCatalogItemsResponse(after)),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<ReorderPositionDescriptionCatalogItemsResponse>.Success(
                new ReorderPositionDescriptionCatalogItemsResponse(after));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
