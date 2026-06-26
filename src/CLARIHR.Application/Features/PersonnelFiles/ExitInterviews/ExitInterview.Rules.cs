using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Coded errors for the exit-interview form builder (RF-007). Structural-coherence failures (duplicate
/// keys, options/range coherence, empty form, wrong lifecycle state) surface as these codes; simple
/// per-field validation (weight ≥ 0, key pattern, lengths) stays as <c>common.validation</c> (400) via
/// FluentValidation. Every code below must have a matching entry in BackendMessages.resx and
/// BackendMessages.es.resx (parity enforced by <c>BackendMessageLocalizationTests</c>).
/// </summary>
internal static class ExitInterviewErrors
{
    public static readonly Error FormNameDuplicate = new(
        "EXIT_INTERVIEW_FORM_NAME_DUPLICATE",
        "An exit-interview form with the same name already exists.",
        ErrorType.Conflict);

    public static readonly Error FormNotDraft = new(
        "EXIT_INTERVIEW_FORM_NOT_DRAFT",
        "The exit-interview form can only be edited while it is in draft.",
        ErrorType.Conflict);

    public static readonly Error FormNotPublished = new(
        "EXIT_INTERVIEW_FORM_NOT_PUBLISHED",
        "The exit-interview form must be published for this operation.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FormNotPublishable = new(
        "EXIT_INTERVIEW_FORM_NOT_PUBLISHABLE",
        "The exit-interview form must have at least one field to be published.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FieldKeyDuplicate = new(
        "EXIT_INTERVIEW_FIELD_KEY_DUPLICATE",
        "The form already has a field with the same key.",
        ErrorType.Conflict);

    public static readonly Error OptionCodeDuplicate = new(
        "EXIT_INTERVIEW_OPTION_CODE_DUPLICATE",
        "The field already has an option with the same code.",
        ErrorType.Conflict);

    public static readonly Error FieldOptionsRequired = new(
        "EXIT_INTERVIEW_FIELD_OPTIONS_REQUIRED",
        "A selection field must define at least one option.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FieldOptionsNotAllowed = new(
        "EXIT_INTERVIEW_FIELD_OPTIONS_NOT_ALLOWED",
        "This control type does not support options.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FieldRangeInvalid = new(
        "EXIT_INTERVIEW_FIELD_RANGE_INVALID",
        "The field minimum cannot be greater than its maximum.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FieldRangeNotAllowed = new(
        "EXIT_INTERVIEW_FIELD_RANGE_NOT_ALLOWED",
        "This control type does not support a numeric range.",
        ErrorType.UnprocessableEntity);

    public static readonly Error OptionsNotAllowedOnField = new(
        "EXIT_INTERVIEW_OPTIONS_NOT_ALLOWED_ON_FIELD",
        "Options can only be added to a selection control type.",
        ErrorType.UnprocessableEntity);

    public static readonly Error FormNotFound = new(
        "EXIT_INTERVIEW_FORM_NOT_FOUND",
        "The exit-interview form does not exist.",
        ErrorType.NotFound);

    public static readonly Error FormConcurrencyConflict = new(
        "EXIT_INTERVIEW_FORM_CONCURRENCY_CONFLICT",
        "The exit-interview form was modified by someone else. Reload and try again.",
        ErrorType.Conflict);

    public static readonly Error ControlTypeInvalid = new(
        "EXIT_INTERVIEW_CONTROL_TYPE_INVALID",
        "The field control type is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error SubmissionNotFound = new(
        "EXIT_INTERVIEW_SUBMISSION_NOT_FOUND",
        "The exit-interview submission does not exist.",
        ErrorType.NotFound);

    public static readonly Error SubmissionAlreadySubmitted = new(
        "EXIT_INTERVIEW_SUBMISSION_ALREADY_SUBMITTED",
        "The exit interview has already been submitted and can no longer be changed.",
        ErrorType.Conflict);
}

/// <summary>
/// Pure coherence rules for an exit-interview form definition (RF-007). The handler resolves the control
/// type's capabilities from the <c>form-control-types</c> catalog and loads sibling keys/options, then
/// calls these pure functions — keeping every check unit-testable without a database.
/// </summary>
internal static class ExitInterviewRules
{
    internal sealed record ExistingFieldKey(Guid PublicId, string NormalizedFieldKey);

    internal sealed record ExistingOptionCode(Guid PublicId, string NormalizedOptionCode);

    /// <summary>A field for whole-definition (publish-time) validation, with its resolved capabilities.</summary>
    internal sealed record FieldDefinition(
        bool SupportsOptions,
        bool SupportsRange,
        decimal? MinValue,
        decimal? MaxValue,
        int ActiveOptionCount);

    /// <summary>The field key is unique within the form (candidate excludes itself on update).</summary>
    public static Result CheckFieldKeyUnique(
        Guid? candidatePublicId,
        string normalizedFieldKey,
        IReadOnlyCollection<ExistingFieldKey> siblings)
    {
        var duplicate = siblings.Any(sibling =>
            sibling.PublicId != candidatePublicId
            && string.Equals(sibling.NormalizedFieldKey, normalizedFieldKey, StringComparison.Ordinal));
        return duplicate ? Result.Failure(ExitInterviewErrors.FieldKeyDuplicate) : Result.Success();
    }

    /// <summary>The option code is unique within the field (candidate excludes itself on update).</summary>
    public static Result CheckOptionCodeUnique(
        Guid? candidatePublicId,
        string normalizedOptionCode,
        IReadOnlyCollection<ExistingOptionCode> siblings)
    {
        var duplicate = siblings.Any(sibling =>
            sibling.PublicId != candidatePublicId
            && string.Equals(sibling.NormalizedOptionCode, normalizedOptionCode, StringComparison.Ordinal));
        return duplicate ? Result.Failure(ExitInterviewErrors.OptionCodeDuplicate) : Result.Success();
    }

    /// <summary>
    /// Per-field config coherence checked at add/update time, given the control type's capabilities:
    /// a numeric range is only allowed when the control type supports it, and min ≤ max.
    /// (Option presence is checked at publish time, since options are added after the field.)
    /// </summary>
    public static Result CheckFieldConfig(bool supportsRange, decimal? minValue, decimal? maxValue)
    {
        if (!supportsRange && (minValue.HasValue || maxValue.HasValue))
        {
            return Result.Failure(ExitInterviewErrors.FieldRangeNotAllowed);
        }

        if (minValue.HasValue && maxValue.HasValue && minValue.Value > maxValue.Value)
        {
            return Result.Failure(ExitInterviewErrors.FieldRangeInvalid);
        }

        return Result.Success();
    }

    /// <summary>Options may only be attached to a control type that supports them (RF-006).</summary>
    public static Result CheckOptionsAllowed(bool fieldSupportsOptions) =>
        fieldSupportsOptions ? Result.Success() : Result.Failure(ExitInterviewErrors.OptionsNotAllowedOnField);

    /// <summary>
    /// Whole-definition validation required before publishing (RF-007/008): at least one field; every
    /// selection field has ≥ 1 active option; non-selection fields have none; numeric ranges are coherent.
    /// Returns the first failure (or success).
    /// </summary>
    public static Result ValidateDefinitionForPublish(IReadOnlyCollection<FieldDefinition> fields)
    {
        if (fields.Count == 0)
        {
            return Result.Failure(ExitInterviewErrors.FormNotPublishable);
        }

        foreach (var field in fields)
        {
            if (field.SupportsOptions && field.ActiveOptionCount == 0)
            {
                return Result.Failure(ExitInterviewErrors.FieldOptionsRequired);
            }

            if (!field.SupportsOptions && field.ActiveOptionCount > 0)
            {
                return Result.Failure(ExitInterviewErrors.FieldOptionsNotAllowed);
            }

            var configResult = CheckFieldConfig(field.SupportsRange, field.MinValue, field.MaxValue);
            if (configResult.IsFailure)
            {
                return configResult;
            }
        }

        return Result.Success();
    }
}
