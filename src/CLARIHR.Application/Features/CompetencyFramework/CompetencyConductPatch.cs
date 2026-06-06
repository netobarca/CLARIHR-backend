using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.CompetencyFramework;

// ─── Command ─────────────────────────────────────────────────────────────────

public sealed record CompetencyConductPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchCompetencyConductCommand(
    Guid ConductId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<CompetencyConductPatchOperation> Operations)
    : ICommand<CompetencyConductResponse>;

internal sealed class PatchCompetencyConductCommandValidator
    : AbstractValidator<PatchCompetencyConductCommand>
{
    public PatchCompetencyConductCommandValidator()
    {
        RuleFor(command => command.ConductId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

// ─── Handler ─────────────────────────────────────────────────────────────────

internal sealed class PatchCompetencyConductCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchCompetencyConductCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        PatchCompetencyConductCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var conduct = await repository.GetCompetencyConductByIdAsync(command.ConductId, includeBehaviors: true, cancellationToken);
        if (conduct is null)
        {
            return Result<CompetencyConductResponse>.Failure(
                await repository.CompetencyConductExistsOutsideTenantAsync(command.ConductId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.CompetencyConductNotFound);
        }

        if (conduct.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        // The response carries the public catalog ids (the entity only holds internal ids), so the patch
        // state is seeded from it and reused as the audit "before".
        var before = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Competency conduct response could not be resolved before patch.");

        var state = CompetencyConductPatchState.From(before);

        var applied = CompetencyConductPatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(applied.Error);
        }

        var validation = CompetencyConductPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            return Result<CompetencyConductResponse>.Success(before);
        }

        // Re-resolve the catalog references exactly like the PUT path: a patched reference must point at
        // an active catalog item inside this tenant (otherwise NotFound / TenantMismatch).
        var competencyResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            conduct.TenantId,
            state.CompetencyId,
            JobCatalogCategory.Competency,
            repository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (competencyResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(competencyResolution.Error);
        }

        var typeResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            conduct.TenantId,
            state.CompetencyTypeId,
            JobCatalogCategory.CompetencyType,
            repository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (typeResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(typeResolution.Error);
        }

        var levelResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            conduct.TenantId,
            state.BehaviorLevelId,
            JobCatalogCategory.BehaviorLevel,
            repository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (levelResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(levelResolution.Error);
        }

        // A patched description or reference can collide with another conduct in the same tenant; re-run
        // the same per-tenant 4-field tuple uniqueness check the PUT path performs (excludes this conduct).
        var normalizedDescription = state.Description.Trim().ToUpperInvariant();
        if (await repository.CompetencyConductDuplicateExistsAsync(
                conduct.TenantId,
                competencyResolution.Value.Id,
                typeResolution.Value.Id,
                levelResolution.Value.Id,
                normalizedDescription,
                conduct.Id,
                cancellationToken))
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.CompetencyConductDuplicate);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            conduct.Update(
                competencyResolution.Value.Id,
                typeResolution.Value.Id,
                levelResolution.Value.Id,
                state.Description,
                state.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyConductUpdated,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Update,
                    $"Updated competency conduct {conduct.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ─── Patch state + applier ─────────────────────────────────────────────────────

internal sealed class CompetencyConductPatchState
{
    public Guid CompetencyId { get; set; }
    public Guid CompetencyTypeId { get; set; }
    public Guid BehaviorLevelId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool HasMutation { get; set; }

    public static CompetencyConductPatchState From(CompetencyConductResponse response) =>
        new()
        {
            CompetencyId = response.CompetencyId,
            CompetencyTypeId = response.CompetencyTypeId,
            BehaviorLevelId = response.BehaviorLevelId,
            Description = response.Description,
            SortOrder = response.SortOrder
        };
}

internal sealed class CompetencyConductPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class CompetencyConductPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<CompetencyConductPatchOperation> operations, CompetencyConductPatchState state)
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
                return ValidationFailure(operation.Path, "Only root competency conduct properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (CompetencyConductPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(CompetencyConductPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Mirror the command validators + the domain normalizer (trim then enforce the rule) so an invalid
        // patched value is a 400 instead of an unmapped ArgumentException → HTTP 500.
        if (state.CompetencyId == Guid.Empty)
        {
            errors["competencyPublicId"] = ["Competency is required."];
        }

        if (state.CompetencyTypeId == Guid.Empty)
        {
            errors["competencyTypePublicId"] = ["Competency type is required."];
        }

        if (state.BehaviorLevelId == Guid.Empty)
        {
            errors["behaviorLevelPublicId"] = ["Behavior level is required."];
        }

        if (string.IsNullOrWhiteSpace(state.Description))
        {
            errors["description"] = ["Description is required."];
        }
        else if (state.Description.Trim().Length > 1000)
        {
            errors["description"] = ["Description must be 1000 characters or fewer."];
        }

        if (state.SortOrder < 0)
        {
            errors["sortOrder"] = ["Sort order must be zero or greater."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        CompetencyConductPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "concurrencyToken"))
        {
            return ValidationFailure(path, "The concurrency token cannot be patched; send the current token in the If-Match header.");
        }

        // Activation state changes go through the dedicated /activate and /inactivate actions.
        if (IsSegment(property, "isActive") || IsSegment(property, "status"))
        {
            return ValidationFailure(path, "Use the /activate and /inactivate actions to change the active state.");
        }

        // Behaviors are a child collection replaced through the dedicated behaviors endpoint.
        if (IsSegment(property, "behaviors"))
        {
            return ValidationFailure(path, "Use the behaviors endpoint to replace the conduct's behaviors.");
        }

        if (IsSegment(property, "id") || IsSegment(property, "publicId") || IsSegment(property, "companyId") ||
            IsSegment(property, "createdAtUtc") || IsSegment(property, "modifiedAtUtc") || IsSegment(property, "allowedActions"))
        {
            return ValidationFailure(path, "This property is read-only and cannot be patched.");
        }

        if (IsSegment(property, "competencyPublicId"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Competency cannot be removed.");
            }

            state.CompetencyId = ReadRequiredGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "competencyTypePublicId"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Competency type cannot be removed.");
            }

            state.CompetencyTypeId = ReadRequiredGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "behaviorLevelPublicId"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Behavior level cannot be removed.");
            }

            state.BehaviorLevelId = ReadRequiredGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "description"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Description cannot be removed.");
            }

            state.Description = ReadRequiredString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "sortOrder"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Sort order cannot be removed.");
            }

            state.SortOrder = ReadRequiredInt(value, path);
            state.HasMutation = true;
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

    private static Guid ReadRequiredGuid(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new CompetencyConductPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String && Guid.TryParse(value.Value.GetString(), out var parsed)
            ? parsed
            : throw new CompetencyConductPatchValueException(path, "Value must be a GUID.");
    }

    private static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new CompetencyConductPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new CompetencyConductPatchValueException(path, "Value must be a string.");
    }

    private static int ReadRequiredInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new CompetencyConductPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed)
            ? parsed
            : throw new CompetencyConductPatchValueException(path, "Value must be an integer.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}
