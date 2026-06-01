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

internal static class PersonnelFilePreviousEmploymentPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<PersonnelFilePreviousEmploymentPatchOperation> operations, PersonnelFilePreviousEmploymentPatchState state)
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
                return ValidationFailure(operation.Path, "Only root previous employment properties can be patched.");
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

    public static Result Validate(PersonnelFilePreviousEmploymentPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.Institution))
        {
            errors["institution"] = ["Institution is required."];
        }

        if (string.IsNullOrWhiteSpace(state.CurrencyCode))
        {
            errors["currencyCode"] = ["CurrencyCode is required."];
        }

        if (state.RetirementDate.HasValue && state.RetirementDate.Value.Date < state.EntryDate.Date)
        {
            errors["retirementDate"] = ["RetirementDate cannot be earlier than EntryDate."];
        }

        if (state.FirstSalaryAmount is < 0)
        {
            errors["firstSalaryAmount"] = ["FirstSalaryAmount cannot be negative."];
        }

        if (state.LastSalaryAmount is < 0)
        {
            errors["lastSalaryAmount"] = ["LastSalaryAmount cannot be negative."];
        }

        if (state.AverageCommissionAmount is < 0)
        {
            errors["averageCommissionAmount"] = ["AverageCommissionAmount cannot be negative."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFilePreviousEmploymentPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "institution"))
        {
            return Mutate(state, () => state.Institution = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        if (IsSegment(property, "place"))
        {
            return Mutate(state, () => state.Place = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "lastPosition"))
        {
            return Mutate(state, () => state.LastPosition = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "managerName"))
        {
            return Mutate(state, () => state.ManagerName = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "entryDate"))
        {
            return isRemove
                ? ValidationFailure(path, "EntryDate cannot be removed.")
                : Mutate(state, () => state.EntryDate = ReadRequiredDateTime(value, path));
        }

        if (IsSegment(property, "retirementDate"))
        {
            return Mutate(state, () => state.RetirementDate = isRemove ? null : ReadRequiredDateTime(value, path));
        }

        if (IsSegment(property, "companyPhone"))
        {
            return Mutate(state, () => state.CompanyPhone = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "exitReason"))
        {
            return Mutate(state, () => state.ExitReason = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "firstSalaryAmount"))
        {
            return Mutate(state, () => state.FirstSalaryAmount = isRemove ? null : ReadNullableDecimal(value, path));
        }

        if (IsSegment(property, "lastSalaryAmount"))
        {
            return Mutate(state, () => state.LastSalaryAmount = isRemove ? null : ReadNullableDecimal(value, path));
        }

        if (IsSegment(property, "averageCommissionAmount"))
        {
            return Mutate(state, () => state.AverageCommissionAmount = isRemove ? null : ReadNullableDecimal(value, path));
        }

        if (IsSegment(property, "currencyCode"))
        {
            return Mutate(state, () => state.CurrencyCode = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static Result Mutate(PersonnelFilePreviousEmploymentPatchState state, Action apply)
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

