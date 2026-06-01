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

internal static class PersonnelFileFamilyMemberPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<PersonnelFileFamilyMemberPatchOperation> operations, PersonnelFileFamilyMemberPatchState state)
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
                return ValidationFailure(operation.Path, "Only root family member properties can be patched.");
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

    public static Result Validate(PersonnelFileFamilyMemberPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state.FirstName))
        {
            errors["firstName"] = ["FirstName is required."];
        }

        if (string.IsNullOrWhiteSpace(state.LastName))
        {
            errors["lastName"] = ["LastName is required."];
        }

        if (string.IsNullOrWhiteSpace(state.KinshipCode))
        {
            errors["kinshipCode"] = ["KinshipCode is required."];
        }

        if (state.Salary is < 0)
        {
            errors["salary"] = ["Salary cannot be negative."];
        }

        if (state.IsStudying && (string.IsNullOrWhiteSpace(state.StudyPlace) || string.IsNullOrWhiteSpace(state.AcademicLevel)))
        {
            errors["isStudying"] = [PersonnelFileErrors.FamilyMemberRuleViolation.Message];
        }

        if (state.IsWorking && (string.IsNullOrWhiteSpace(state.Workplace) || string.IsNullOrWhiteSpace(state.JobTitle)))
        {
            errors["isWorking"] = [PersonnelFileErrors.FamilyMemberRuleViolation.Message];
        }

        if (state.IsDeceased && !state.DeceasedDate.HasValue)
        {
            errors["isDeceased"] = [PersonnelFileErrors.FamilyMemberRuleViolation.Message];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        PersonnelFileFamilyMemberPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsSegment(property, "firstName"))
        {
            return Mutate(state, () => state.FirstName = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        if (IsSegment(property, "lastName"))
        {
            return Mutate(state, () => state.LastName = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        if (IsSegment(property, "kinshipCode"))
        {
            return Mutate(state, () => state.KinshipCode = isRemove ? string.Empty : ReadRequiredString(value, path));
        }

        if (IsSegment(property, "nationality"))
        {
            return Mutate(state, () => state.Nationality = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "birthDate"))
        {
            return Mutate(state, () => state.BirthDate = isRemove ? null : ReadRequiredDateTime(value, path));
        }

        if (IsSegment(property, "sex"))
        {
            return isRemove
                ? ValidationFailure(path, "Sex cannot be removed.")
                : Mutate(state, () => state.Sex = ReadSex(value, path));
        }

        if (IsSegment(property, "maritalStatus"))
        {
            return Mutate(state, () => state.MaritalStatus = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "occupation"))
        {
            return Mutate(state, () => state.Occupation = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "documentType"))
        {
            return Mutate(state, () => state.DocumentType = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "documentNumber"))
        {
            return Mutate(state, () => state.DocumentNumber = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "phone"))
        {
            return Mutate(state, () => state.Phone = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "isStudying"))
        {
            return isRemove
                ? ValidationFailure(path, "IsStudying cannot be removed.")
                : Mutate(state, () => state.IsStudying = ReadBool(value, path));
        }

        if (IsSegment(property, "studyPlace"))
        {
            return Mutate(state, () => state.StudyPlace = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "academicLevel"))
        {
            return Mutate(state, () => state.AcademicLevel = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "isBeneficiary"))
        {
            return isRemove
                ? ValidationFailure(path, "IsBeneficiary cannot be removed.")
                : Mutate(state, () => state.IsBeneficiary = ReadBool(value, path));
        }

        if (IsSegment(property, "isWorking"))
        {
            return isRemove
                ? ValidationFailure(path, "IsWorking cannot be removed.")
                : Mutate(state, () => state.IsWorking = ReadBool(value, path));
        }

        if (IsSegment(property, "workplace"))
        {
            return Mutate(state, () => state.Workplace = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "jobTitle"))
        {
            return Mutate(state, () => state.JobTitle = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "workPhone"))
        {
            return Mutate(state, () => state.WorkPhone = isRemove ? null : ReadNullableString(value, path));
        }

        if (IsSegment(property, "salary"))
        {
            return Mutate(state, () => state.Salary = isRemove ? null : ReadNullableDecimal(value, path));
        }

        if (IsSegment(property, "isDeceased"))
        {
            return isRemove
                ? ValidationFailure(path, "IsDeceased cannot be removed.")
                : Mutate(state, () => state.IsDeceased = ReadBool(value, path));
        }

        if (IsSegment(property, "deceasedDate"))
        {
            return Mutate(state, () => state.DeceasedDate = isRemove ? null : ReadRequiredDateTime(value, path));
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static Result Mutate(PersonnelFileFamilyMemberPatchState state, Action apply)
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
        if (!string.IsNullOrWhiteSpace(raw) && decimal.TryParse(raw, out parsed))
        {
            return parsed;
        }

        throw new PersonnelFilePatchValueException(path, "Value must be a number.");
    }

    private static PersonnelFamilyMemberSex ReadSex(JsonElement? value, string path)
    {
        var raw = ReadNullableString(value, path);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new PersonnelFilePatchValueException(path, "Sex is required.");
        }

        return Enum.TryParse<PersonnelFamilyMemberSex>(raw, ignoreCase: true, out var parsed) &&
               Enum.IsDefined(typeof(PersonnelFamilyMemberSex), parsed)
            ? parsed
            : throw new PersonnelFilePatchValueException(path, $"Sex '{raw}' is not a valid value.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

