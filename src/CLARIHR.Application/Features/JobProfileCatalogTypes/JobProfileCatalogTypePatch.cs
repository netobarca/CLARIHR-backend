using System.Text.Json;
using CLARIHR.Application.Abstractions.CatalogTypes;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.JobProfileCatalogTypes.Common;
using CLARIHR.Domain.CatalogTypes;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfileCatalogTypes;

// ─── Command ─────────────────────────────────────────────────────────────────

public sealed record JobProfileCatalogTypePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileCatalogTypeCommand(
    Guid Id,
    Guid ConcurrencyToken,
    IReadOnlyCollection<JobProfileCatalogTypePatchOperation> Operations)
    : ICommand<JobProfileCatalogTypeResponse>;

internal sealed class PatchJobProfileCatalogTypeCommandValidator
    : AbstractValidator<PatchJobProfileCatalogTypeCommand>
{
    public PatchJobProfileCatalogTypeCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ConcurrencyToken).NotEmpty();
        RuleFor(c => c.Operations).NotEmpty();
        RuleFor(c => c.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(c => c.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

// ─── Handler ─────────────────────────────────────────────────────────────────

internal sealed class PatchJobProfileCatalogTypeCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICatalogTypeDescriptorRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchJobProfileCatalogTypeCommand, JobProfileCatalogTypeResponse>
{
    public async Task<Result<JobProfileCatalogTypeResponse>> Handle(
        PatchJobProfileCatalogTypeCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.ConcurrencyConflict);
        }

        var state = JobProfileCatalogTypePatchState.From(entity);

        var applied = JobProfileCatalogTypePatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(applied.Error);
        }

        var validation = JobProfileCatalogTypePatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<JobProfileCatalogTypeResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            var unchanged = await repository.GetResponseByIdAsync(command.Id, cancellationToken);
            return unchanged is null
                ? Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound)
                : Result<JobProfileCatalogTypeResponse>.Success(unchanged);
        }

        // Key (Code) is immutable: re-apply the persisted code so it cannot change.
        // name/sortOrder are re-normalized + re-validated by Update exactly as the PUT path does;
        // status transitions go through /activate and /inactivate.
        entity.Update(entity.Code, state.Name, state.SortOrder);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        repository.Invalidate();

        var response = await repository.GetResponseByIdAsync(command.Id, cancellationToken);
        return response is null
            ? Result<JobProfileCatalogTypeResponse>.Failure(JobProfileCatalogTypeErrors.NotFound)
            : Result<JobProfileCatalogTypeResponse>.Success(response);
    }
}

// ─── Patch state + applier ─────────────────────────────────────────────────────

internal sealed class JobProfileCatalogTypePatchState
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileCatalogTypePatchState From(CatalogTypeDescriptor entity) =>
        new()
        {
            Name = entity.Name,
            SortOrder = entity.SortOrder
        };
}

internal sealed class JobProfileCatalogTypePatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class JobProfileCatalogTypePatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileCatalogTypePatchOperation> operations, JobProfileCatalogTypePatchState state)
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
                return ValidationFailure(operation.Path, "Only root catalog type properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (JobProfileCatalogTypePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(JobProfileCatalogTypePatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Mirror the command validators + the domain normalizer (SystemScopedCatalogItem.Clean trims
        // then enforces the max length, throwing on empty/over-length). Validating here keeps an invalid
        // patched value as a 400 instead of an unmapped ArgumentException → 500.
        if (string.IsNullOrWhiteSpace(state.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (state.Name.Trim().Length > 200)
        {
            errors["name"] = ["Name must be 200 characters or fewer."];
        }

        if (state.SortOrder < 0)
        {
            errors["sortOrder"] = ["Sort order cannot be negative."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        JobProfileCatalogTypePatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "concurrencyToken"))
        {
            return ValidationFailure(path, "The concurrency token cannot be patched; send the current token in the If-Match header.");
        }

        // The Code key is immutable once created.
        if (IsSegment(property, "code"))
        {
            return ValidationFailure(path, "Code is immutable and cannot be patched.");
        }

        // Activation state changes go through the dedicated /activate and /inactivate actions.
        if (IsSegment(property, "isActive") || IsSegment(property, "status"))
        {
            return ValidationFailure(path, "Use the /activate and /inactivate actions to change the active state.");
        }

        if (IsSegment(property, "id") || IsSegment(property, "publicId") ||
            IsSegment(property, "createdAtUtc") || IsSegment(property, "modifiedAtUtc"))
        {
            return ValidationFailure(path, "This property is read-only and cannot be patched.");
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

    private static string ReadRequiredString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new JobProfileCatalogTypePatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new JobProfileCatalogTypePatchValueException(path, "Value must be a string.");
    }

    private static int ReadRequiredInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new JobProfileCatalogTypePatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed)
            ? parsed
            : throw new JobProfileCatalogTypePatchValueException(path, "Value must be an integer.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}
