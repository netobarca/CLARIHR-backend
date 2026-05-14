using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record JobCatalogItemResponse(
    Guid Id,
    JobCatalogCategory Category,
    string Code,
    string Name,
    bool IsSystem,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record SearchJobCatalogItemsQuery(
    Guid CompanyId,
    JobCatalogCategory Category,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = JobProfileValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<JobCatalogItemResponse>>;

public sealed record CreateJobCatalogItemCommand(
    Guid CompanyId,
    JobCatalogCategory Category,
    string Code,
    string Name) : ICommand<JobCatalogItemResponse>;

public sealed record UpdateJobCatalogItemCommand(
    Guid CompanyId,
    JobCatalogCategory Category,
    Guid ItemId,
    string Code,
    string Name,
    bool IsActive,
    Guid ConcurrencyToken) : ICommand<JobCatalogItemResponse>;

public sealed record JobCatalogItemPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobCatalogItemCommand(
    Guid CompanyId,
    JobCatalogCategory Category,
    Guid ItemId,
    IReadOnlyCollection<JobCatalogItemPatchOperation> Operations) : ICommand<JobCatalogItemResponse>;

public sealed record RemoveJobCatalogItemCommand(
    Guid CompanyId,
    JobCatalogCategory Category,
    Guid ItemId,
    Guid ConcurrencyToken) : ICommand<JobCatalogItemResponse>;

internal sealed class SearchJobCatalogItemsQueryValidator : AbstractValidator<SearchJobCatalogItemsQuery>
{
    public SearchJobCatalogItemsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, JobProfileValidationRules.MaxPageSize);
    }
}

internal sealed class CreateJobCatalogItemCommandValidator : AbstractValidator<CreateJobCatalogItemCommand>
{
    public CreateJobCatalogItemCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
    }
}

internal sealed class UpdateJobCatalogItemCommandValidator : AbstractValidator<UpdateJobCatalogItemCommand>
{
    public UpdateJobCatalogItemCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class PatchJobCatalogItemCommandValidator : AbstractValidator<PatchJobCatalogItemCommand>
{
    public PatchJobCatalogItemCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(ContainsConcurrencyToken)
            .WithMessage("Patch document must include a non-remove operation for /concurrencyToken.");
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }

    private static bool ContainsConcurrencyToken(IReadOnlyCollection<JobCatalogItemPatchOperation>? operations) =>
        operations is not null &&
        operations.Any(static operation =>
            !string.Equals(operation.Op, "remove", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(operation.Path) &&
            string.Equals(operation.Path.Trim(), "/concurrencyToken", StringComparison.OrdinalIgnoreCase));
}

internal sealed class RemoveJobCatalogItemCommandValidator : AbstractValidator<RemoveJobCatalogItemCommand>
{
    public RemoveJobCatalogItemCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchJobCatalogItemsQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobCatalogRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchJobCatalogItemsQuery, PagedResponse<JobCatalogItemResponse>>
{
    public async Task<Result<PagedResponse<JobCatalogItemResponse>>> Handle(
        SearchJobCatalogItemsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<JobCatalogItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.Category,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<JobCatalogItemResponse>>.Success(response);
        }

        var canManageCatalogs = (await authorizationService.EnsureCanManageCatalogsAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => JobCatalogPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManageCatalogs))
            .ToArray();

        return Result<PagedResponse<JobCatalogItemResponse>>.Success(response with { Items = items });
    }
}

internal static class JobCatalogPolicyAdapter
{
    public static JobCatalogItemResponse ApplyAllowedActions(
        JobCatalogItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManageCatalogs)
    {
        var state = response.Category.ToString();
        var canMutateSystem = !response.IsSystem && canManageCatalogs;
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                JobProfilePermissionCodes.ResourceKey,
                state,
                response.IsActive,
                IsSystem: response.IsSystem,
                SupportsEdit: true,
                EditAllowed: canMutateSystem,
                SupportsDelete: true,
                DeleteAllowed: canMutateSystem,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canMutateSystem,
                SupportsInactivate: true,
                InactivateAllowed: canMutateSystem));

        return response with { AllowedActions = allowedActions };
    }
}

internal sealed class CreateJobCatalogItemCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateJobCatalogItemCommand, JobCatalogItemResponse>
{
    public async Task<Result<JobCatalogItemResponse>> Handle(
        CreateJobCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageCatalogsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Category,
                command.Code.Trim().ToUpperInvariant(),
                excludingItemId: null,
                cancellationToken))
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.CatalogCodeConflict);
        }

        var item = JobCatalogItem.Create(command.Category, command.Code, command.Name);
        item.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(item);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobCatalogItemCreated,
                    AuditEntityTypes.JobCatalogItem,
                    item.PublicId,
                    item.Code,
                    AuditActions.Create,
                    $"Created job catalog item {item.Code} ({item.Category}).",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            repository.InvalidateCategoryCache(command.CompanyId, command.Category);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobCatalogItemResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobCatalogItemCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateJobCatalogItemCommand, JobCatalogItemResponse>
{
    public async Task<Result<JobCatalogItemResponse>> Handle(
        UpdateJobCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageCatalogsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var item = await repository.GetByIdAsync(command.ItemId, cancellationToken);
        if (item is null || item.Category != command.Category)
        {
            return Result<JobCatalogItemResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.ItemId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.CatalogItemNotFound);
        }

        if (item.IsSystem)
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.CatalogItemSystemImmutable);
        }

        if (item.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var normalizedIncomingCode = command.Code.Trim().ToUpperInvariant();
        if (item.NormalizedCode != normalizedIncomingCode &&
            await repository.CodeExistsAsync(
                command.CompanyId,
                command.Category,
                normalizedIncomingCode,
                excludingItemId: item.Id,
                cancellationToken))
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.CatalogCodeConflict);
        }

        var before = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Catalog item response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            item.Update(command.Code, command.Name);
            if (command.IsActive && !item.IsActive)
            {
                item.Activate();
            }
            else if (!command.IsActive && item.IsActive)
            {
                item.Inactivate();
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobCatalogItemUpdated,
                    AuditEntityTypes.JobCatalogItem,
                    item.PublicId,
                    item.Code,
                    AuditActions.Update,
                    $"Updated job catalog item {item.Code} ({item.Category}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            repository.InvalidateCategoryCache(item.TenantId, item.Category);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobCatalogItemCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchJobCatalogItemCommand, JobCatalogItemResponse>
{
    public async Task<Result<JobCatalogItemResponse>> Handle(
        PatchJobCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageCatalogsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var item = await repository.GetByIdAsync(command.ItemId, cancellationToken);
        if (item is null || item.Category != command.Category)
        {
            return Result<JobCatalogItemResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.ItemId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.CatalogItemNotFound);
        }

        if (item.IsSystem)
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.CatalogItemSystemImmutable);
        }

        var before = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Catalog item response could not be resolved before patch.");
        var patchState = JobCatalogItemPatchState.From(before);
        var patchApplication = JobCatalogItemPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobCatalogItemResponse>.Failure(patchApplication.Error);
        }

        var validation = JobCatalogItemPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobCatalogItemResponse>.Failure(validation.Error);
        }

        if (patchState.ConcurrencyToken != item.ConcurrencyToken)
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobCatalogItemResponse>.Success(before);
        }

        var normalizedIncomingCode = patchState.Code.Trim().ToUpperInvariant();
        if (item.NormalizedCode != normalizedIncomingCode &&
            await repository.CodeExistsAsync(
                command.CompanyId,
                command.Category,
                normalizedIncomingCode,
                excludingItemId: item.Id,
                cancellationToken))
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.CatalogCodeConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            item.Update(patchState.Code, patchState.Name);
            if (patchState.IsActive && !item.IsActive)
            {
                item.Activate();
            }
            else if (!patchState.IsActive && item.IsActive)
            {
                item.Inactivate();
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobCatalogItemUpdated,
                    AuditEntityTypes.JobCatalogItem,
                    item.PublicId,
                    item.Code,
                    AuditActions.Update,
                    $"Patched job catalog item {item.Code} ({item.Category}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            repository.InvalidateCategoryCache(item.TenantId, item.Category);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobCatalogItemCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobCatalogRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobCatalogItemCommand, JobCatalogItemResponse>
{
    public async Task<Result<JobCatalogItemResponse>> Handle(
        RemoveJobCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageCatalogsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var item = await repository.GetByIdAsync(command.ItemId, cancellationToken);
        if (item is null || item.Category != command.Category)
        {
            return Result<JobCatalogItemResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.ItemId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.CatalogItemNotFound);
        }

        if (item.IsSystem)
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.CatalogItemSystemImmutable);
        }

        if (item.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        if (await repository.HasUsageAsync(item.Id, cancellationToken))
        {
            return Result<JobCatalogItemResponse>.Failure(JobProfileErrors.CatalogItemInUse);
        }

        var before = await repository.GetResponseByIdAsync(item.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Catalog item response could not be resolved before delete.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Remove(item);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobCatalogItemUpdated,
                    AuditEntityTypes.JobCatalogItem,
                    item.PublicId,
                    item.Code,
                    AuditActions.Update,
                    $"Deleted job catalog item {item.Code} ({item.Category}).",
                    Before: before),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            repository.InvalidateCategoryCache(item.TenantId, item.Category);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobCatalogItemResponse>.Success(before);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class JobCatalogItemPatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public bool ConcurrencyTokenTouched { get; set; }
    public bool HasMutation { get; set; }

    public static JobCatalogItemPatchState From(JobCatalogItemResponse response) =>
        new()
        {
            Code = response.Code,
            Name = response.Name,
            IsActive = response.IsActive,
            ConcurrencyToken = response.ConcurrencyToken
        };
}

internal static class JobCatalogItemPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobCatalogItemPatchOperation> operations, JobCatalogItemPatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!SupportedOperations.Contains(op))
            {
                return ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = ParsePath(operation.Path);
            if (segments.Length != 1)
            {
                return ValidationFailure(operation.Path, "Only root catalog item properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (JobProfilePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(JobCatalogItemPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!state.ConcurrencyTokenTouched)
        {
            errors["concurrencyToken"] = ["ConcurrencyToken is required."];
        }
        else if (state.ConcurrencyToken == Guid.Empty)
        {
            errors["concurrencyToken"] = ["ConcurrencyToken must be a valid UUID."];
        }

        if (string.IsNullOrWhiteSpace(state.Code))
        {
            errors["code"] = ["Code is required."];
        }
        else if (state.Code.Length > 50)
        {
            errors["code"] = ["Code must be 50 characters or fewer."];
        }
        else if (!JobProfileValidationRules.IsValidCode(state.Code))
        {
            errors["code"] = ["Code format is invalid."];
        }

        if (string.IsNullOrWhiteSpace(state.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (state.Name.Length > 120)
        {
            errors["name"] = ["Name must be 120 characters or fewer."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        JobCatalogItemPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "code"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Code cannot be removed.");
            }

            state.Code = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "name"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Name cannot be removed.");
            }

            state.Name = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "isActive"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "IsActive cannot be removed.");
            }

            state.IsActive = ReadBool(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "concurrencyToken"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "ConcurrencyToken cannot be removed.");
            }

            state.ConcurrencyToken = ReadRequiredGuid(value, path);
            state.ConcurrencyTokenTouched = true;
            return Result.Success();
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static string[] ParsePath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapeJsonPointerSegment)
            .ToArray();

    private static string UnescapeJsonPointerSegment(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

    private static bool IsNull(JsonElement? value) =>
        !value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static bool IsSegment(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new JobProfilePatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new JobProfilePatchValueException(path, "Value must be a string.");
    }

    private static bool ReadBool(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new JobProfilePatchValueException(path, "Value must be a boolean.");
        }

        return value!.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.Value.GetString(), out var parsed) => parsed,
            _ => throw new JobProfilePatchValueException(path, "Value must be a boolean.")
        };
    }

    private static Guid ReadRequiredGuid(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new JobProfilePatchValueException(path, "Value must be a valid UUID.");
        }

        var raw = value!.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JobProfilePatchValueException(path, "Value must be a valid UUID.");
        }

        return Guid.TryParse(raw, out var parsed)
            ? parsed
            : throw new JobProfilePatchValueException(path, "Value must be a valid UUID.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}
