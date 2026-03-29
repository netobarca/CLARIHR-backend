using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.Common;

public static partial class PersonnelFileValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidName(string value) =>
        NameRegex().IsMatch(value.Trim());

    public static bool IsValidCode(string value) =>
        CodeRegex().IsMatch(value.Trim());

    public static bool IsValidPhone(string value) =>
        PhoneRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^[\p{L}][\p{L}\p{N} '.-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_./-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"^[+0-9][0-9\- ]{5,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();

    public static int CalculateAge(DateTime birthDate, DateTime utcNow)
    {
        var today = utcNow.Date;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    public static Error ValidateCustomData(
        IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse> definitions,
        string? customDataJson)
    {
        if (string.IsNullOrWhiteSpace(customDataJson))
        {
            var requiredWithoutPayload = definitions
                .Where(static definition => definition.IsActive && definition.IsRequired)
                .Select(static definition => definition.Key)
                .ToArray();

            return requiredWithoutPayload.Length == 0
                ? Error.None
                : ErrorCatalog.Validation(
                    new Dictionary<string, string[]>
                    {
                        ["customData"] = [$"Missing required fields: {string.Join(", ", requiredWithoutPayload)}"]
                    });
        }

        try
        {
            using var document = global::System.Text.Json.JsonDocument.Parse(customDataJson);
            if (document.RootElement.ValueKind != global::System.Text.Json.JsonValueKind.Object)
            {
                return PersonnelFileErrors.CustomDataInvalid;
            }

            var payload = document.RootElement;
            foreach (var definition in definitions.Where(static definition => definition.IsActive))
            {
                var hasProperty = payload.TryGetProperty(definition.Key, out var property);
                if (!hasProperty)
                {
                    if (definition.IsRequired)
                    {
                        return ErrorCatalog.Validation(
                            new Dictionary<string, string[]>
                            {
                                ["customData"] = [$"Required custom field '{definition.Key}' is missing."]
                            });
                    }

                    continue;
                }

                if (property.ValueKind == global::System.Text.Json.JsonValueKind.Null)
                {
                    if (definition.IsRequired)
                    {
                        return ErrorCatalog.Validation(
                            new Dictionary<string, string[]>
                            {
                                ["customData"] = [$"Required custom field '{definition.Key}' cannot be null."]
                            });
                    }

                    continue;
                }

                if (!IsCustomFieldTypeValid(definition.FieldType, property))
                {
                    return ErrorCatalog.Validation(
                        new Dictionary<string, string[]>
                        {
                            ["customData"] = [$"Custom field '{definition.Key}' has invalid value type."]
                        });
                }
            }

            return Error.None;
        }
        catch
        {
            return PersonnelFileErrors.CustomDataInvalid;
        }
    }

    private static bool IsCustomFieldTypeValid(PersonnelCustomFieldType fieldType, global::System.Text.Json.JsonElement property)
    {
        return fieldType switch
        {
            PersonnelCustomFieldType.String => property.ValueKind == global::System.Text.Json.JsonValueKind.String,
            PersonnelCustomFieldType.Number => property.ValueKind == global::System.Text.Json.JsonValueKind.Number,
            PersonnelCustomFieldType.Date => property.ValueKind == global::System.Text.Json.JsonValueKind.String,
            PersonnelCustomFieldType.Bool => property.ValueKind is global::System.Text.Json.JsonValueKind.True or global::System.Text.Json.JsonValueKind.False,
            PersonnelCustomFieldType.Select => property.ValueKind == global::System.Text.Json.JsonValueKind.String,
            _ => false
        };
    }
}

public static class PersonnelFilePermissionCodes
{
    public const string Read = "PersonnelFiles.Read";
    public const string Admin = "PersonnelFiles.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "PERSONNEL_FILES";
}

public static class PersonnelFileErrors
{
    public static readonly Error Forbidden = new(
        "PERSONNEL_FILES_FORBIDDEN",
        "You do not have permission to access personnel file administration.",
        ErrorType.Forbidden);

    public static readonly Error NotFound = new(
        "PERSONNEL_FILE_NOT_FOUND",
        "The personnel file could not be found.",
        ErrorType.NotFound);

    public static readonly Error IdentificationConflict = new(
        "PERSONNEL_FILE_IDENTIFICATION_CONFLICT",
        "Another personnel file already uses the requested identification.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error StateRuleViolation = new(
        "PERSONNEL_FILE_STATE_RULE_VIOLATION",
        "The requested operation is not allowed for the current personnel file state.",
        ErrorType.UnprocessableEntity);

    public static readonly Error HireEndpointRequired = new(
        "PERSONNEL_FILE_HIRE_ENDPOINT_REQUIRED",
        "Candidate to employee conversion is only allowed through the hire endpoint.",
        ErrorType.UnprocessableEntity);

    public static readonly Error EffectiveDatesInvalid = new(
        "PERSONNEL_FILE_EFFECTIVE_DATES_INVALID",
        "The date range is invalid.",
        ErrorType.UnprocessableEntity);

    public static readonly Error DocumentNotFound = new(
        "PERSONNEL_FILE_DOCUMENT_NOT_FOUND",
        "The personnel file document could not be found.",
        ErrorType.NotFound);

    public static readonly Error DocumentFileRequired = new(
        "PERSONNEL_FILE_DOCUMENT_FILE_REQUIRED",
        "A document file is required.",
        ErrorType.Validation);

    public static readonly Error DocumentLoanDatesInvalid = new(
        "PERSONNEL_FILE_DOCUMENT_DATES_INVALID",
        "Document loan and return dates are invalid.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ExportFormatInvalid = new(
        "PERSONNEL_FILE_EXPORT_FORMAT_INVALID",
        "Unsupported export format.",
        ErrorType.Validation);

    public static readonly Error CustomFieldDefinitionNotFound = new(
        "PERSONNEL_CUSTOM_FIELD_DEFINITION_NOT_FOUND",
        "The custom field definition could not be found.",
        ErrorType.NotFound);

    public static readonly Error CustomFieldKeyConflict = new(
        "PERSONNEL_CUSTOM_FIELD_KEY_CONFLICT",
        "Another custom field definition already uses the requested key.",
        ErrorType.Conflict);

    public static readonly Error CustomDataInvalid = new(
        "PERSONNEL_CUSTOM_DATA_INVALID",
        "The custom data payload is invalid.",
        ErrorType.Validation);

    public static readonly Error FamilyMemberRuleViolation = new(
        "PERSONNEL_FILE_FAMILY_MEMBER_RULE_VIOLATION",
        "Family member conditional fields are invalid.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(PersonnelFilePermissionCodes.ResourceKey, action);
}
