using System.Text.Json;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.DocumentTypeCatalogs.Common;
using CLARIHR.Domain.DocumentTypeCatalogs;
using FluentValidation;

namespace CLARIHR.Application.Features.DocumentTypeCatalogs;

// ─── Command ─────────────────────────────────────────────────────────────────

public sealed record DocumentTypeCatalogItemPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchDocumentTypeCatalogItemCommand(
    Guid Id,
    Guid ConcurrencyToken,
    IReadOnlyCollection<DocumentTypeCatalogItemPatchOperation> Operations)
    : ICommand<DocumentTypeCatalogItemResponse>;

internal sealed class PatchDocumentTypeCatalogItemCommandValidator
    : AbstractValidator<PatchDocumentTypeCatalogItemCommand>
{
    public PatchDocumentTypeCatalogItemCommandValidator()
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

internal sealed class PatchDocumentTypeCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    IDocumentTypeCatalogRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchDocumentTypeCatalogItemCommand, DocumentTypeCatalogItemResponse>
{
    public async Task<Result<DocumentTypeCatalogItemResponse>> Handle(
        PatchDocumentTypeCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authResult.IsFailure)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(authResult.Error);
        }

        var entity = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (entity is null)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.ConcurrencyConflict);
        }

        var state = DocumentTypeCatalogItemPatchState.From(entity);

        var applied = DocumentTypeCatalogItemPatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(applied.Error);
        }

        var validation = DocumentTypeCatalogItemPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            var unchanged = await repository.GetResponseByIdAsync(command.Id, cancellationToken);
            return unchanged is null
                ? Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound)
                : Result<DocumentTypeCatalogItemResponse>.Success(unchanged);
        }

        var normalizedCode = state.Code.Trim().ToUpperInvariant();
        if (await repository.CodeExistsAsync(normalizedCode, entity.Id, cancellationToken))
        {
            return Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.CodeConflict);
        }

        // Scalar-only patch: code/name/sortOrder are the editable fields and are re-normalized +
        // re-validated by Update exactly as the PUT path does. Status transitions go through
        // /activate and /inactivate, not this patch.
        entity.Update(state.Code, state.Name, state.SortOrder);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await repository.GetResponseByIdAsync(command.Id, cancellationToken);
        return response is null
            ? Result<DocumentTypeCatalogItemResponse>.Failure(DocumentTypeCatalogErrors.NotFound)
            : Result<DocumentTypeCatalogItemResponse>.Success(response);
    }
}

// ─── Patch state + applier ─────────────────────────────────────────────────────

internal sealed class DocumentTypeCatalogItemPatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool HasMutation { get; set; }

    public static DocumentTypeCatalogItemPatchState From(DocumentTypeCatalogItem entity) =>
        new()
        {
            Code = entity.Code,
            Name = entity.Name,
            SortOrder = entity.SortOrder
        };
}

internal sealed class DocumentTypeCatalogItemPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class DocumentTypeCatalogItemPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<DocumentTypeCatalogItemPatchOperation> operations, DocumentTypeCatalogItemPatchState state)
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
            catch (DocumentTypeCatalogItemPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(DocumentTypeCatalogItemPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Mirror the command validators + the domain normalizer (SystemScopedCatalogItem.Clean trims
        // then enforces the max length, throwing on empty/over-length). Validating here keeps an invalid
        // patched value as a 400 instead of an unmapped ArgumentException → 500.
        if (string.IsNullOrWhiteSpace(state.Code))
        {
            errors["code"] = ["Code is required."];
        }
        else if (!DocumentTypeCatalogValidationRules.IsValidCode(state.Code))
        {
            errors["code"] = ["Code format is invalid."];
        }

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
        DocumentTypeCatalogItemPatchState state,
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

        if (IsSegment(property, "id") || IsSegment(property, "publicId") ||
            IsSegment(property, "createdAtUtc") || IsSegment(property, "modifiedAtUtc"))
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
            throw new DocumentTypeCatalogItemPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new DocumentTypeCatalogItemPatchValueException(path, "Value must be a string.");
    }

    private static int ReadRequiredInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new DocumentTypeCatalogItemPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed)
            ? parsed
            : throw new DocumentTypeCatalogItemPatchValueException(path, "Value must be an integer.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}
