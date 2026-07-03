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

internal static class PersonnelFilePatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<PersonnelFilePatchOperation> operations, PersonnelFilePatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!SupportedOperations.Contains(op))
            {
                return ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = ParsePath(operation.Path);
            if (segments.Length == 0)
            {
                return ValidationFailure(operation.Path, "Patch path is required.");
            }

            if (segments.Length != 1)
            {
                return ValidationFailure(operation.Path, "Only root patch paths are supported.");
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

    private static Result ApplyOperation(string op, string property, JsonElement? value, PersonnelFilePatchState state, string path)
    {
        var isRemove = IsRemove(op);

        if (IsSegment(property, "recordType"))
        {
            return isRemove
                ? ValidationFailure(path, "RecordType cannot be removed.")
                : SetAndSucceed(() => state.RecordType = ReadRecordType(value, path));
        }

        if (IsSegment(property, "firstName"))
        {
            state.FirstName = isRemove ? string.Empty : ReadRequiredString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "lastName"))
        {
            state.LastName = isRemove ? string.Empty : ReadRequiredString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "birthDate"))
        {
            return isRemove
                ? ValidationFailure(path, "BirthDate cannot be removed.")
                : SetAndSucceed(() => state.BirthDate = ReadRequiredDateTime(value, path));
        }

        if (IsSegment(property, "maritalStatusCode"))
        {
            state.MaritalStatusCode = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "professionCode"))
        {
            state.ProfessionCode = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "personalTitleCode"))
        {
            state.PersonalTitleCode = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "afpCode"))
        {
            state.AfpCode = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "nationality"))
        {
            state.Nationality = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "personalEmail"))
        {
            state.PersonalEmail = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "institutionalEmail"))
        {
            state.InstitutionalEmail = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "personalPhone"))
        {
            state.PersonalPhone = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "institutionalPhone"))
        {
            state.InstitutionalPhone = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "birthCountryCode"))
        {
            state.BirthCountryCode = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "birthDepartmentCode"))
        {
            state.BirthDepartmentCode = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "birthMunicipalityCode"))
        {
            state.BirthMunicipalityCode = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsAnySegment(property, "photoFilePublicId", "photoFileId"))
        {
            state.PhotoFilePublicId = isRemove ? null : ReadNullableGuid(value, path);
            return Result.Success();
        }

        if (IsAnySegment(property, "orgUnitPublicId", "orgUnitId"))
        {
            state.OrgUnitPublicId = isRemove ? null : ReadNullableGuid(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "isActive"))
        {
            return isRemove
                ? ValidationFailure(path, "IsActive cannot be removed.")
                : SetAndSucceed(() => state.IsActive = ReadBool(value, path));
        }

        if (IsSegment(property, "isRehireBlocked"))
        {
            return isRemove
                ? ValidationFailure(path, "IsRehireBlocked cannot be removed.")
                : SetAndSucceed(() => state.IsRehireBlocked = ReadBool(value, path));
        }

        if (IsSegment(property, "rehireBlockedReason"))
        {
            state.RehireBlockedReason = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static Result SetAndSucceed(Action set)
    {
        set();
        return Result.Success();
    }

    private static PersonnelFileRecordType ReadRecordType(JsonElement? value, string path)
    {
        var raw = ReadNullableString(value, path);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new PersonnelFilePatchValueException(path, "RecordType is required.");
        }

        return Enum.TryParse<PersonnelFileRecordType>(raw, ignoreCase: true, out var parsed) &&
               Enum.IsDefined(typeof(PersonnelFileRecordType), parsed)
            ? parsed
            : throw new PersonnelFilePatchValueException(path, $"RecordType '{raw}' is not a valid value.");
    }

    private static string[] ParsePath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapeJsonPointerSegment)
            .ToArray();

    private static string UnescapeJsonPointerSegment(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

    private static bool IsRemove(string op) => string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

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

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));
}

