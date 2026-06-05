using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Banks.Common;
using CLARIHR.Domain.Banks;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.Banks;

// ─── Command ─────────────────────────────────────────────────────────────────

public sealed record BankCatalogItemPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchBankCatalogItemCommand(
    Guid BankPublicId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<BankCatalogItemPatchOperation> Operations)
    : ICommand<BankCatalogItemResponse>;

internal sealed class PatchBankCatalogItemCommandValidator
    : AbstractValidator<PatchBankCatalogItemCommand>
{
    public PatchBankCatalogItemCommandValidator()
    {
        RuleFor(command => command.BankPublicId).NotEmpty();
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

internal sealed class PatchBankCatalogItemCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICountryCatalogRepository countryCatalogRepository,
    IBankCatalogRepository repository,
    IPlatformAuditService platformAuditService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILogger<PatchBankCatalogItemCommandHandler> logger)
    : ICommandHandler<PatchBankCatalogItemCommand, BankCatalogItemResponse>
{
    public async Task<Result<BankCatalogItemResponse>> Handle(
        PatchBankCatalogItemCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<BankCatalogItemResponse>.Failure(authorizationResult.Error);
        }

        var bank = await repository.GetByIdAsync(command.BankPublicId, cancellationToken);
        if (bank is null)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.NotFound);
        }

        if (bank.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.ConcurrencyConflict);
        }

        var state = BankCatalogItemPatchState.From(bank);

        var applied = BankCatalogItemPatchApplier.Apply(command.Operations, state);
        if (applied.IsFailure)
        {
            return Result<BankCatalogItemResponse>.Failure(applied.Error);
        }

        var validation = BankCatalogItemPatchApplier.Validate(state);
        if (validation.IsFailure)
        {
            return Result<BankCatalogItemResponse>.Failure(validation.Error);
        }

        if (!state.HasMutation)
        {
            var unchanged = await repository.GetResponseByIdAsync(bank.PublicId, cancellationToken);
            return unchanged is null
                ? Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.NotFound)
                : Result<BankCatalogItemResponse>.Success(unchanged);
        }

        // The country is immutable (the applier rejects /countryCode); re-resolve the bank's existing
        // country to obtain its internal id, mirroring the PUT path.
        var country = await countryCatalogRepository.GetActiveByCodeAsync(bank.CountryCode, cancellationToken);
        if (country is null)
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.CountryNotFound(bank.CountryCode));
        }

        if (await repository.ExistsByCodeAsync(country.InternalId, state.Code.Trim().ToUpperInvariant(), bank.Id, cancellationToken))
        {
            return Result<BankCatalogItemResponse>.Failure(BankCatalogErrors.CodeConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = await repository.GetResponseByIdAsync(bank.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Bank catalog response could not be resolved before update.");

            bank.Update(
                country.InternalId,
                country.Code,
                state.Code,
                state.Name,
                state.Alias,
                state.SwiftCode,
                state.RoutingCode,
                state.SortOrder);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(bank.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Bank catalog response could not be resolved after update.");

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.BankCatalogItemUpdated,
                    AuditEntityTypes.BankCatalogItem,
                    bank.PublicId,
                    bank.Code,
                    AuditActions.Update,
                    $"Updated bank catalog item {bank.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Bank catalog item {BankCode} patched by user {UserId}.",
                bank.Code,
                currentUserService.UserId);

            return Result<BankCatalogItemResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ─── Patch state + applier ─────────────────────────────────────────────────────

internal sealed class BankCatalogItemPatchState
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public string? SwiftCode { get; set; }
    public string? RoutingCode { get; set; }
    public int SortOrder { get; set; }
    public bool HasMutation { get; set; }

    public static BankCatalogItemPatchState From(BankCatalogItem bank) =>
        new()
        {
            Code = bank.Code,
            Name = bank.Name,
            Alias = bank.Alias,
            SwiftCode = bank.SwiftCode,
            RoutingCode = bank.RoutingCode,
            SortOrder = bank.SortOrder
        };
}

internal sealed class BankCatalogItemPatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal static class BankCatalogItemPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<BankCatalogItemPatchOperation> operations, BankCatalogItemPatchState state)
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
                return ValidationFailure(operation.Path, "Only root bank catalog properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (BankCatalogItemPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(BankCatalogItemPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Mirror the command validators + the domain normalizer (Clean/CleanOptional trim then enforce the
        // max length, throwing on empty/over-length). Validating here keeps an invalid patched value a 400
        // instead of an unmapped ArgumentException → 500.
        if (string.IsNullOrWhiteSpace(state.Code))
        {
            errors["code"] = ["Code is required."];
        }
        else if (!BankCatalogValidationRules.IsValidCode(state.Code))
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

        if (state.Alias is not null && state.Alias.Trim().Length > 120)
        {
            errors["alias"] = ["Alias must be 120 characters or fewer."];
        }

        if (state.SwiftCode is not null && state.SwiftCode.Trim().Length > 40)
        {
            errors["swiftCode"] = ["Swift code must be 40 characters or fewer."];
        }

        if (state.RoutingCode is not null && state.RoutingCode.Trim().Length > 40)
        {
            errors["routingCode"] = ["Routing code must be 40 characters or fewer."];
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
        BankCatalogItemPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "concurrencyToken"))
        {
            return ValidationFailure(path, "The concurrency token cannot be patched; send the current token in the If-Match header.");
        }

        // The country is the catalog scope and cannot be changed on an existing item.
        if (IsSegment(property, "countryCode"))
        {
            return ValidationFailure(path, "Changing the country of an existing bank catalog item is not supported.");
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

        if (IsSegment(property, "alias"))
        {
            state.Alias = isRemove ? null : ReadOptionalString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "swiftCode"))
        {
            state.SwiftCode = isRemove ? null : ReadOptionalString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "routingCode"))
        {
            state.RoutingCode = isRemove ? null : ReadOptionalString(value, path);
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
            throw new BankCatalogItemPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString() ?? string.Empty
            : throw new BankCatalogItemPatchValueException(path, "Value must be a string.");
    }

    private static string? ReadOptionalString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new BankCatalogItemPatchValueException(path, "Value must be a string or null.");
    }

    private static int ReadRequiredInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new BankCatalogItemPatchValueException(path, "Value is required.");
        }

        return value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed)
            ? parsed
            : throw new BankCatalogItemPatchValueException(path, "Value must be an integer.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}
