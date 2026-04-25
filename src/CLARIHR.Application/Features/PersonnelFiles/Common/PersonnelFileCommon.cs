using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.Common;

public static partial class PersonnelFileValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxDocumentFileSizeBytes = 10 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string> AllowedDocumentContentTypesByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

    public static bool IsValidName(string value) =>
        NameRegex().IsMatch(value.Trim());

    public static bool IsValidCode(string value) =>
        CodeRegex().IsMatch(value.Trim());

    public static bool IsValidPhone(string value) =>
        PhoneRegex().IsMatch(value.Trim());

    public static bool IsAllowedDocumentExtension(string fileName) =>
        AllowedDocumentContentTypesByExtension.ContainsKey(Path.GetExtension(fileName));

    public static bool IsAllowedDocumentContentType(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        return AllowedDocumentContentTypesByExtension.TryGetValue(extension, out var expectedContentType) &&
               string.Equals(expectedContentType, contentType.Trim(), StringComparison.OrdinalIgnoreCase);
    }

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

    public static readonly Error RecordTypeTransitionNotAllowed = new(
        "PERSONNEL_FILE_RECORD_TYPE_TRANSITION_NOT_ALLOWED",
        "Personnel file record type transitions are not allowed in this module.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ProvisioningFieldsLocked = new(
        "PERSONNEL_FILE_PROVISIONING_FIELDS_LOCKED",
        "Assigned position slot and institutional email cannot be changed after completion.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FinalizeRequiresInstitutionalEmail = new(
        "PERSONNEL_FILE_FINALIZE_REQUIRES_INSTITUTIONAL_EMAIL",
        "An institutional email is required to finalize the personnel file.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FinalizeRequiresAssignedPositionSlot = new(
        "PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT",
        "An assigned position slot is required to finalize the personnel file.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FinalizeRequiresPositionSlotRole = new(
        "PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT_ROLE",
        "The assigned position slot must have a valid role configured before finalizing the personnel file.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FinalizeOnlyEmployee = new(
        "PERSONNEL_FILE_FINALIZE_ONLY_EMPLOYEE",
        "Only employee personnel files can be finalized.",
        ErrorType.UnprocessableEntity);

    public static readonly Error LinkedUserConflict = new(
        "PERSONNEL_FILE_LINKED_USER_CONFLICT",
        "The institutional email is already linked to a different personnel file.",
        ErrorType.Conflict);

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

    public static readonly Error DocumentFileTooLarge = new(
        "PERSONNEL_FILE_DOCUMENT_TOO_LARGE",
        "The document file exceeds the maximum allowed size.",
        ErrorType.PayloadTooLarge);

    public static readonly Error DocumentContentTypeUnsupported = new(
        "PERSONNEL_FILE_DOCUMENT_CONTENT_TYPE_UNSUPPORTED",
        "The document file type is not supported.",
        ErrorType.Validation);

    public static readonly Error DocumentStorageNotConfigured = new(
        "PERSONNEL_FILE_DOCUMENT_STORAGE_NOT_CONFIGURED",
        "Personnel file document storage is not configured.",
        ErrorType.ServiceUnavailable);

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
