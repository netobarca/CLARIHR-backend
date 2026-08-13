using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

using CLARIHR.Application.Common.Policies;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record JobCatalogItemOrderItemResponse(
    Guid Id,
    string Code,
    int SortOrder);

public sealed record ReorderJobCatalogItemsResponse(
    IReadOnlyList<JobCatalogItemOrderItemResponse> Items,
    // H-11/H-33 — el marcador que el guardrail `OptedInControllers_ReturnMarkerTypeOnEveryPutPatchAction`
    // exige a toda acción PUT/PATCH de un controlador con `[ResourceActions]`. Faltaba desde H-11, y el
    // guardrail llevaba rojo desde entonces sin que nadie lo viera: la suite completa no se corría.
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

/// <summary>
/// H-11 — bulk reorder of one job catalog category. Same contract as its siblings: the complete set of ids in
/// the desired order, server-assigned numbers, single write phase (this order is not unique). The category only
/// gained an ordering field in this same finding, so without this endpoint reordering it would have been N
/// patches from day one — the very asymmetry the finding is about.
/// </summary>
public sealed record ReorderJobCatalogItemsCommand(
    Guid CompanyId,
    JobCatalogCategory Category,
    IReadOnlyList<Guid> OrderedPublicIds) : ICommand<ReorderJobCatalogItemsResponse>;

internal sealed class ReorderJobCatalogItemsCommandValidator : AbstractValidator<ReorderJobCatalogItemsCommand>
{
    public ReorderJobCatalogItemsCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.OrderedPublicIds).NotEmpty();
        RuleForEach(command => command.OrderedPublicIds).NotEmpty();
    }
}

internal sealed class ReorderJobCatalogItemsCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReorderJobCatalogItemsCommand, ReorderJobCatalogItemsResponse>
{
    public async Task<Result<ReorderJobCatalogItemsResponse>> Handle(
        ReorderJobCatalogItemsCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageCatalogsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<ReorderJobCatalogItemsResponse>.Failure(authorizationResult.Error);
        }

        var items = await repository.GetAllByCategoryAsync(command.CompanyId, command.Category, cancellationToken);
        if (items.Count == 0)
        {
            return Result<ReorderJobCatalogItemsResponse>.Failure(JobProfileErrors.CatalogOrderSetIncomplete);
        }

        var permutationResult = CatalogBulkReorder.EnsureIsCompletePermutation(
            command.OrderedPublicIds,
            items.Select(item => item.PublicId).ToArray(),
            JobProfileErrors.CatalogOrderSetIncomplete);
        if (permutationResult.IsFailure)
        {
            return Result<ReorderJobCatalogItemsResponse>.Failure(permutationResult.Error);
        }

        var byPublicId = items.ToDictionary(item => item.PublicId);
        var ordered = command.OrderedPublicIds.Select(id => byPublicId[id]).ToArray();
        var before = items
            .Select(item => new JobCatalogItemOrderItemResponse(item.PublicId, item.Code, item.SortOrder))
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            for (var index = 0; index < ordered.Length; index++)
            {
                ordered[index].SetSortOrder(CatalogBulkReorder.OrderAt(index));
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            repository.InvalidateCategoryCache(command.CompanyId, command.Category);

            var after = ordered
                .Select(item => new JobCatalogItemOrderItemResponse(item.PublicId, item.Code, item.SortOrder))
                .ToArray();

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobCatalogItemUpdated,
                    AuditEntityTypes.JobCatalogItem,
                    command.CompanyId,
                    $"{command.Category}: {ordered.Length} items",
                    AuditActions.Update,
                    $"Reordered {ordered.Length} {command.Category} job catalog item(s).",
                    Before: new ReorderJobCatalogItemsResponse(before),
                    After: new ReorderJobCatalogItemsResponse(after)),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<ReorderJobCatalogItemsResponse>.Success(new ReorderJobCatalogItemsResponse(after));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
