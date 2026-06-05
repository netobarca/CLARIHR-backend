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
using CLARIHR.Domain.CompetencyFramework;
using FluentValidation;

namespace CLARIHR.Application.Features.CompetencyFramework;

// ─── Command ─────────────────────────────────────────────────────────────────

public sealed record OccupationalPyramidLevelPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchOccupationalPyramidLevelCommand(
    Guid LevelId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<OccupationalPyramidLevelPatchOperation> Operations)
    : ICommand<OccupationalPyramidLevelResponse>;

internal sealed class PatchOccupationalPyramidLevelCommandValidator
    : AbstractValidator<PatchOccupationalPyramidLevelCommand>
{
    public PatchOccupationalPyramidLevelCommandValidator()
    {
        RuleFor(command => command.LevelId).NotEmpty();
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

internal sealed class PatchOccupationalPyramidLevelCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchOccupationalPyramidLevelCommand, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        PatchOccupationalPyramidLevelCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetOccupationalPyramidLevelByIdAsync(command.LevelId, cancellationToken);
        if (level is null)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(
                await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(command.LevelId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
        }

        if (level.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var state = OccupationalPyramidLevelPatchState.From(level);

        var applied = OccupationalPyramidLevelPatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(applied.Error);
        }

        var validation = OccupationalPyramidLevelPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            var unchanged = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken);
            return unchanged is null
                ? Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound)
                : Result<OccupationalPyramidLevelResponse>.Success(unchanged);
        }

        // A patched code or level order can collide with another row in the same tenant; re-run the
        // same per-tenant uniqueness checks the PUT path performs (both exclude this level by id).
        var normalizedCode = state.Code.Trim().ToUpperInvariant();
        if (await repository.OccupationalPyramidLevelCodeExistsAsync(level.TenantId, normalizedCode, level.Id, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelCodeConflict);
        }

        if (await repository.OccupationalPyramidLevelOrderExistsAsync(level.TenantId, state.LevelOrder, level.Id, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelOrderConflict);
        }

        var before = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            level.Update(state.Code, state.Name, state.LevelOrder, state.Description);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OccupationalPyramidLevelUpdated,
                    AuditEntityTypes.OccupationalPyramidLevel,
                    level.PublicId,
                    level.Code,
                    AuditActions.Update,
                    $"Updated occupational pyramid level {level.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OccupationalPyramidLevelResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ─── Patch state + applier ─────────────────────────────────────────────────────

internal sealed class OccupationalPyramidLevelPatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int LevelOrder { get; set; }
    public string? Description { get; set; }
    public bool HasMutation { get; set; }

    public static OccupationalPyramidLevelPatchState From(OccupationalPyramidLevel level) =>
        new()
        {
            Code = level.Code,
            Name = level.Name,
            LevelOrder = level.LevelOrder,
            Description = level.Description
        };
}

internal sealed class OccupationalPyramidLevelPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class OccupationalPyramidLevelPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<OccupationalPyramidLevelPatchOperation> operations, OccupationalPyramidLevelPatchState state)
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
                return ValidationFailure(operation.Path, "Only root occupational pyramid level properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (OccupationalPyramidLevelPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(OccupationalPyramidLevelPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Mirror the command validators + the domain normalizer (trim then enforce the rule) so an
        // invalid patched value is a 400 instead of an unmapped ArgumentException → HTTP 500.
        if (string.IsNullOrWhiteSpace(state.Code))
        {
            errors["code"] = ["Code is required."];
        }
        else if (!CompetencyFrameworkValidationRules.IsValidCode(state.Code))
        {
            errors["code"] = ["Code format is invalid."];
        }

        if (string.IsNullOrWhiteSpace(state.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (state.Name.Trim().Length > 120)
        {
            errors["name"] = ["Name must be 120 characters or fewer."];
        }

        if (state.LevelOrder <= 0)
        {
            errors["levelOrder"] = ["Level order must be greater than zero."];
        }

        if (state.Description is not null && state.Description.Trim().Length > 500)
        {
            errors["description"] = ["Description must be 500 characters or fewer."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        OccupationalPyramidLevelPatchState state,
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

        if (IsSegment(property, "id") || IsSegment(property, "publicId") || IsSegment(property, "companyId") ||
            IsSegment(property, "createdAtUtc") || IsSegment(property, "modifiedAtUtc") || IsSegment(property, "allowedActions"))
        {
            return ValidationFailure(path, "This property is read-only and cannot be patched.");
        }

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

        if (IsSegment(property, "levelOrder"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Level order cannot be removed.");
            }

            state.LevelOrder = ReadRequiredInt(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "description"))
        {
            state.Description = isRemove ? null : ReadOptionalString(value, path);
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

    private static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new OccupationalPyramidLevelPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new OccupationalPyramidLevelPatchValueException(path, "Value must be a string.");
    }

    private static string? ReadOptionalString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new OccupationalPyramidLevelPatchValueException(path, "Value must be a string or null.");
    }

    private static int ReadRequiredInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new OccupationalPyramidLevelPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed)
            ? parsed
            : throw new OccupationalPyramidLevelPatchValueException(path, "Value must be an integer.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}
