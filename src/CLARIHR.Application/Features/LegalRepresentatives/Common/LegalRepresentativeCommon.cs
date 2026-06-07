using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.LegalRepresentatives;
using FluentValidation;

namespace CLARIHR.Application.Features.LegalRepresentatives.Common;

public sealed record InitialLegalRepresentativeInput(
    string FirstName,
    string LastName,
    string DocumentType,
    string DocumentNumber,
    string PositionTitle,
    LegalRepresentativeRepresentationType RepresentationType,
    string? AuthorityDescription,
    string? AppointmentInstrument,
    DateTime? AppointmentDateUtc,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Email,
    string? Phone,
    bool? IsPrimary = null);

public static partial class LegalRepresentativeValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Max length of <c>DocumentType</c> — pinned to the <c>document_type varchar(40)</c> column
    /// (single source of truth shared by the Create/Update/InitialInput validators). Keeping the
    /// validator at the column width turns an over-length value into a clean 400 instead of an
    /// unmapped PostgreSQL "value too long" → HTTP 500 (audit §LR2).
    /// </summary>
    public const int MaxDocumentTypeLength = 40;

    // §12.8 / §LR3 — free-text search (NormalizedFullName/PositionTitle/DocumentNumber Contains →
    // non-sargable LIKE '%x%') must enforce a minimum trimmed length in the validator (rejected 400
    // before DB). Threshold aligned with the PersonnelFiles §PF1 / PositionSlots §PS2 precedent (2).
    // Scale assumption: legal representatives per tenant are a tiny handful, so the (TenantId, …)
    // scan + min length is comfortably cheap; escalate to pg_trgm GIN + EF.Functions.ILike only if
    // cardinality grows unexpectedly. See project-foundation.md §12.8 / ADR-0002.
    public const int MinSearchLength = 2;

    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;

    public static bool IsValidName(string value) =>
        NameRegex().IsMatch(value.Trim());

    public static bool IsValidDocumentNumber(string value) =>
        DocumentRegex().IsMatch(value.Trim());

    public static bool IsValidPositionTitle(string value) =>
        PositionRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^[\p{L}][\p{L}\p{N} '.-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_./-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentRegex();

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} '&().,/-]{0,149}$", RegexOptions.CultureInvariant)]
    private static partial Regex PositionRegex();
}

public static class LegalRepresentativePermissionCodes
{
    public const string Read = "LegalRepresentatives.Read";
    public const string Admin = "LegalRepresentatives.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "LEGAL_REPRESENTATIVES";
}

public static class LegalRepresentativeErrors
{
    public static readonly Error Forbidden = new(
        "LEGAL_REPRESENTATIVES_FORBIDDEN",
        "You do not have permission to access legal representative administration.",
        ErrorType.Forbidden);

    public static readonly Error NotFound = new(
        "LEGAL_REPRESENTATIVE_NOT_FOUND",
        "The legal representative could not be found.",
        ErrorType.NotFound);

    public static readonly Error DocumentConflict = new(
        "LEGAL_REPRESENTATIVE_DOCUMENT_CONFLICT",
        "Another legal representative already uses the requested document.",
        ErrorType.Conflict);

    public static readonly Error ActiveMinimumRequired = new(
        "LEGAL_REPRESENTATIVE_ACTIVE_MIN_REQUIRED",
        "The company must have at least one active legal representative.",
        ErrorType.Conflict);

    public static readonly Error EffectiveDatesInvalid = new(
        "LEGAL_REPRESENTATIVE_EFFECTIVE_DATES_INVALID",
        "The effective date range is invalid.",
        ErrorType.UnprocessableEntity);

    public static readonly Error StateRuleViolation = new(
        "LEGAL_REPRESENTATIVE_STATE_RULE_VIOLATION",
        "The requested operation is not allowed for the current legal representative state.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error ExportFormatInvalid = new(
        "LEGAL_REPRESENTATIVE_EXPORT_FORMAT_INVALID",
        "Unsupported export format.",
        ErrorType.Validation);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(LegalRepresentativePermissionCodes.ResourceKey, action);
}

internal sealed class InitialLegalRepresentativeInputValidator : AbstractValidator<InitialLegalRepresentativeInput>
{
    public InitialLegalRepresentativeInputValidator()
    {
        RuleFor(input => input.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(LegalRepresentativeValidationRules.IsValidName)
            .WithMessage("FirstName format is invalid.");

        RuleFor(input => input.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .Must(LegalRepresentativeValidationRules.IsValidName)
            .WithMessage("LastName format is invalid.");

        RuleFor(input => input.DocumentType)
            .NotEmpty()
            .MaximumLength(LegalRepresentativeValidationRules.MaxDocumentTypeLength);

        RuleFor(input => input.DocumentNumber)
            .NotEmpty()
            .MaximumLength(80)
            .Must(LegalRepresentativeValidationRules.IsValidDocumentNumber)
            .WithMessage("DocumentNumber format is invalid.");

        RuleFor(input => input.PositionTitle)
            .NotEmpty()
            .MaximumLength(150)
            .Must(LegalRepresentativeValidationRules.IsValidPositionTitle)
            .WithMessage("PositionTitle format is invalid.");

        RuleFor(input => input.AuthorityDescription)
            .MaximumLength(500);

        RuleFor(input => input.AppointmentInstrument)
            .MaximumLength(500);

        RuleFor(input => input.Email)
            .EmailAddress()
            .MaximumLength(320)
            .When(static input => !string.IsNullOrWhiteSpace(input.Email));

        RuleFor(input => input.Phone)
            .MaximumLength(40);

        RuleFor(input => input.EffectiveFromUtc)
            .NotEmpty();

        RuleFor(input => input.EffectiveToUtc)
            .Must((input, to) => !to.HasValue || to.Value.Date >= input.EffectiveFromUtc.Date)
            .WithMessage(LegalRepresentativeErrors.EffectiveDatesInvalid.Message);
    }
}
