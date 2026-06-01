using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;
using FluentValidation.Results;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal static class PersonnelFileTrainingPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<PersonnelFileTrainingPatchOperation> operations, PersonnelFileTrainingPatchState state)
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
                return ValidationFailure(operation.Path, "Only root training properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (PersonnelFilePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(PersonnelFileTrainingPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.TrainingName))
        {
            errors["trainingName"] = ["TrainingName is required."];
        }

        if (string.IsNullOrWhiteSpace(state.TrainingTypeCode))
        {
            errors["trainingTypeCode"] = ["TrainingTypeCode is required."];
        }

        if (string.IsNullOrWhiteSpace(state.CountryCode))
        {
            errors["countryCode"] = ["CountryCode is required."];
        }

        if (string.IsNullOrWhiteSpace(state.DurationUnitCode))
        {
            errors["durationUnitCode"] = ["DurationUnitCode is required."];
        }

        if (state.EndDate.HasValue && state.EndDate.Value.Date < state.StartDate.Date)
        {
            errors["endDate"] = ["EndDate cannot be earlier than StartDate."];
        }

        if (state.DurationValue <= 0)
        {
            errors["durationValue"] = ["DurationValue must be greater than zero."];
        }

        if (state.CostAmount is < 0)
        {
            errors["costAmount"] = ["CostAmount cannot be negative."];
        }

        if (state.CostAmount.HasValue && string.IsNullOrWhiteSpace(state.CostCurrencyCode))
        {
            errors["costCurrencyCode"] = ["CostCurrencyCode is required when CostAmount is provided."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileTrainingPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "trainingName"))
        {
            return Mutate(state, () => state.TrainingName = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        if (IsSegment(property, "trainingTypeCode"))
        {
            return Mutate(state, () => state.TrainingTypeCode = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        if (IsSegment(property, "description"))
        {
            return Mutate(state, () => state.Description = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "topic"))
        {
            return Mutate(state, () => state.Topic = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "institution"))
        {
            return Mutate(state, () => state.Institution = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "instructors"))
        {
            return Mutate(state, () => state.Instructors = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "score"))
        {
            return Mutate(state, () => state.Score = isRemove ? null : ReadNullableDecimal(value, path));
        }

        if (IsSegment(property, "startDate"))
        {
            return isRemove
                ? ValidationFailure(path, "StartDate cannot be removed.")
                : Mutate(state, () => state.StartDate = ReadRequiredDateTime(value, path));
        }

        if (IsSegment(property, "endDate"))
        {
            return Mutate(state, () => state.EndDate = isRemove ? null : ReadRequiredDateTime(value, path));
        }

        if (IsSegment(property, "isInternal"))
        {
            return isRemove
                ? ValidationFailure(path, "IsInternal cannot be removed.")
                : Mutate(state, () => state.IsInternal = ReadBool(value, path));
        }

        if (IsSegment(property, "isLocal"))
        {
            return isRemove
                ? ValidationFailure(path, "IsLocal cannot be removed.")
                : Mutate(state, () => state.IsLocal = ReadBool(value, path));
        }

        if (IsSegment(property, "countryCode"))
        {
            return Mutate(state, () => state.CountryCode = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        if (IsSegment(property, "durationValue"))
        {
            return isRemove
                ? ValidationFailure(path, "DurationValue cannot be removed.")
                : Mutate(state, () => state.DurationValue = ReadRequiredDecimal(value, path));
        }

        if (IsSegment(property, "durationUnitCode"))
        {
            return Mutate(state, () => state.DurationUnitCode = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        if (IsSegment(property, "costAmount"))
        {
            return Mutate(state, () => state.CostAmount = isRemove ? null : ReadNullableDecimal(value, path));
        }

        if (IsSegment(property, "costCurrencyCode"))
        {
            return Mutate(state, () => state.CostCurrencyCode = isRemove ? null : ReadNullableString(value, path));
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static Result Mutate(PersonnelFileTrainingPatchState state, Action apply)
    {
        apply();
        state.HasMutation = true;
        return Result.Success();
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

    private static string ReadRequiredString(JsonElement? value, string path) =>
        ReadNullableString(value, path) ?? string.Empty;

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new PersonnelFilePatchValueException(path, "Value must be a string or null.");
    }

    private static DateTime ReadRequiredDateTime(JsonElement? value, string path)
    {
        if (!IsNull(value) &&
            value!.Value.ValueKind == JsonValueKind.String &&
            value.Value.TryGetDateTime(out var parsed))
        {
            return parsed;
        }

        throw new PersonnelFilePatchValueException(path, "Value must be a valid date-time string.");
    }

    private static bool ReadBool(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new PersonnelFilePatchValueException(path, "Value must be a boolean.");
        }

        return value!.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new PersonnelFilePatchValueException(path, "Value must be a boolean.")
        };
    }

    private static decimal ReadRequiredDecimal(JsonElement? value, string path) =>
        ReadNullableDecimal(value, path)
        ?? throw new PersonnelFilePatchValueException(path, "Value must be a number.");

    private static decimal? ReadNullableDecimal(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        if (value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDecimal(out var parsed))
        {
            return parsed;
        }

        var raw = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (!string.IsNullOrWhiteSpace(raw) &&
            decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        throw new PersonnelFilePatchValueException(path, "Value must be a number.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

