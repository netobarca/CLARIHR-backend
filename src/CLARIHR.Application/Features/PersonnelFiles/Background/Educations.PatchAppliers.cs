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

internal static class PersonnelFileEducationPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<PersonnelFileEducationPatchOperation> operations, PersonnelFileEducationPatchState state)
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
                return ValidationFailure(operation.Path, "Only root education properties can be patched.");
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

    public static Result Validate(PersonnelFileEducationPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (state.StatusPublicId == Guid.Empty)
        {
            errors["statusPublicId"] = ["StatusPublicId must be a valid UUID."];
        }

        if (state.StudyTypePublicId == Guid.Empty)
        {
            errors["studyTypePublicId"] = ["StudyTypePublicId must be a valid UUID."];
        }

        if (state.CareerPublicId == Guid.Empty)
        {
            errors["careerPublicId"] = ["CareerPublicId must be a valid UUID."];
        }

        if (state.ShiftPublicId == Guid.Empty)
        {
            errors["shiftPublicId"] = ["ShiftPublicId must be a valid UUID."];
        }

        if (state.ModalityPublicId == Guid.Empty)
        {
            errors["modalityPublicId"] = ["ModalityPublicId must be a valid UUID."];
        }

        if (string.IsNullOrWhiteSpace(state.Institution))
        {
            errors["institution"] = ["Institution is required."];
        }

        if (string.IsNullOrWhiteSpace(state.CountryCode))
        {
            errors["countryCode"] = ["CountryCode is required."];
        }

        if (!state.IsCurrentlyStudying && !state.EndDate.HasValue)
        {
            errors["endDate"] = ["EndDate is required when IsCurrentlyStudying is false."];
        }

        if (state.EndDate.HasValue && state.EndDate.Value.Date < state.StartDate.Date)
        {
            errors["endDate"] = ["EndDate cannot be earlier than StartDate."];
        }

        if (state.TotalSubjects is < 0)
        {
            errors["totalSubjects"] = ["TotalSubjects cannot be negative."];
        }

        if (state.ApprovedSubjects is < 0)
        {
            errors["approvedSubjects"] = ["ApprovedSubjects cannot be negative."];
        }

        if (state.TotalSubjects.HasValue && state.ApprovedSubjects.HasValue &&
            state.ApprovedSubjects.Value > state.TotalSubjects.Value)
        {
            errors["approvedSubjects"] = ["ApprovedSubjects cannot be greater than TotalSubjects."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileEducationPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsAnySegment(property, "statusPublicId", "statusId"))
        {
            return isRemove
                ? ValidationFailure(path, "StatusPublicId cannot be removed.")
                : Mutate(state, () => state.StatusPublicId = ReadRequiredGuid(value, path));
        }

        if (IsSegment(property, "degreeTitle"))
        {
            return Mutate(state, () => state.DegreeTitle = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsAnySegment(property, "studyTypePublicId", "studyTypeId"))
        {
            return isRemove
                ? ValidationFailure(path, "StudyTypePublicId cannot be removed.")
                : Mutate(state, () => state.StudyTypePublicId = ReadRequiredGuid(value, path));
        }

        if (IsAnySegment(property, "careerPublicId", "careerId"))
        {
            return isRemove
                ? ValidationFailure(path, "CareerPublicId cannot be removed.")
                : Mutate(state, () => state.CareerPublicId = ReadRequiredGuid(value, path));
        }

        if (IsSegment(property, "institution"))
        {
            return Mutate(state, () => state.Institution = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        if (IsSegment(property, "countryCode"))
        {
            return Mutate(state, () => state.CountryCode = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        if (IsSegment(property, "specialty"))
        {
            return Mutate(state, () => state.Specialty = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "isCurrentlyStudying"))
        {
            return isRemove
                ? ValidationFailure(path, "IsCurrentlyStudying cannot be removed.")
                : Mutate(state, () => state.IsCurrentlyStudying = ReadBool(value, path));
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

        if (IsAnySegment(property, "shiftPublicId", "shiftId"))
        {
            return Mutate(state, () => state.ShiftPublicId = isRemove ? null : ReadNullableGuid(value, path));
        }

        if (IsAnySegment(property, "modalityPublicId", "modalityId"))
        {
            return Mutate(state, () => state.ModalityPublicId = isRemove ? null : ReadNullableGuid(value, path));
        }

        if (IsSegment(property, "totalSubjects"))
        {
            return Mutate(state, () => state.TotalSubjects = isRemove ? null : ReadNullableInt(value, path));
        }

        if (IsSegment(property, "approvedSubjects"))
        {
            return Mutate(state, () => state.ApprovedSubjects = isRemove ? null : ReadNullableInt(value, path));
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static Result Mutate(PersonnelFileEducationPatchState state, Action apply)
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

    private static bool IsAnySegment(string actual, params string[] expected) =>
        expected.Any(item => IsSegment(actual, item));

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

    private static Guid ReadRequiredGuid(JsonElement? value, string path) =>
        ReadNullableGuid(value, path)
        ?? throw new PersonnelFilePatchValueException(path, "Value must be a valid UUID.");

    private static Guid? ReadNullableGuid(JsonElement? value, string path)
    {
        var raw = ReadNullableString(value, path);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Guid.TryParse(raw, out var parsed)
            ? parsed
            : throw new PersonnelFilePatchValueException(path, "Value must be a valid UUID.");
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

    private static int? ReadNullableInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        if (value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        var raw = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out parsed))
        {
            return parsed;
        }

        throw new PersonnelFilePatchValueException(path, "Value must be an integer.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

