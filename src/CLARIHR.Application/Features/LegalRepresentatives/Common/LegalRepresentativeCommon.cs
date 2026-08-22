using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.LegalRepresentatives;
using FluentValidation;

namespace CLARIHR.Application.Features.LegalRepresentatives.Common;

/// <summary>
/// El representante con el que nace una empresa.
/// <para>
/// B-04 — <b>no lleva <c>IsPrimary</c> a propósito</b>. Es el único representante de una empresa recién creada,
/// así que sólo hay un valor válido: principal. Un campo que sólo admite un valor es ruido en el contrato, y
/// dejarlo abierto fue el defecto — llegaba <c>null</c> y la empresa nacía sin ningún principal. La garantía es
/// del servidor (<c>CompanyProvisioningService</c>), no del cliente, que además ni siquiera expone el flag (F-06).
/// </para>
/// </summary>
public sealed record InitialLegalRepresentativeInput(
    string FirstName,
    string LastName,
    string DocumentType,
    string DocumentNumber,
    string PositionTitle,
    LegalRepresentativeRepresentationType RepresentationType,
    string? AuthorityDescription,
    string? AppointmentInstrument,
    DateOnly? AppointmentDate,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Email,
    string? Phone);

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

    // 00002 / B-03 — el texto es LITERAL, no interpolado. Con `$"…{MinSearchLength}…"` la clave que el
    // localizador deriva del mensaje se calcula en tiempo de ejecución, así que no se puede verificar de
    // forma estática que exista en el .resx — y no existía: los 37 sitios salían en inglés. Que el número
    // siga coincidiendo con la constante lo comprueba `CodeFormatMessageTests`.
    public const string SearchLengthMessage =
        "Search must be at least 2 characters when provided.";

    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;

    public static bool IsValidName(string value) =>
        NameRegex().IsMatch(value.Trim());

    /// <summary>
    /// Validates the document number against the rules of its own document type. A generic "letters,
    /// digits and separators" check accepts things that cannot exist — a DUI of ten digits, for instance —
    /// and a malformed number is not a harmless typo here: identity is <c>(type, number)</c>, so an
    /// impossible DUI silently becomes a second person for someone who already exists.
    ///
    /// Separators are stripped before matching because they carry no information: the same DUI written
    /// <c>01234567-8</c> or <c>012345678</c> is the same document, and the domain normalizes it the same
    /// way before storing it.
    /// </summary>
    public static bool IsValidDocumentNumber(string? documentType, string value) =>
        IsValidDocumentNumber(countryCode: null, documentType, value);

    /// <summary>
    /// Format rules are keyed by (country, type), not by type alone: a document code only means something
    /// inside its country. DUI is Salvadoran; a Colombian CC has its own shape, and nothing stops two
    /// countries from reusing a code for different documents. Unknown combinations fall back to the generic
    /// alphabet check rather than rejecting — a country we have not written rules for yet must still be
    /// usable.
    /// </summary>
    public static bool IsValidDocumentNumber(string? countryCode, string? documentType, string value)
    {
        var digits = StripSeparators(value);
        if (digits.Length == 0)
        {
            return false;
        }

        var key = $"{NormalizeDocumentType(countryCode)}:{NormalizeDocumentType(documentType)}";

        return key switch
        {
            // DUI: 8 digits plus one check digit. Art. 3 Ley Especial Reguladora del DUI.
            "SV:DUI" => DuiRegex().IsMatch(digits),

            // NIT: ####-######-###-#. Same shape the employer NIT already enforces in Compliance.
            "SV:NIT" => NitRegex().IsMatch(digits),

            // Passports, residence cards and every country without dedicated rules yet.
            _ => DocumentRegex().IsMatch(value.Trim())
        };
    }

    /// <summary>
    /// Drops every non-alphanumeric character. Shared by the validator and the uniqueness comparison so
    /// both agree on what "the same document" means.
    /// </summary>
    public static string StripSeparators(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : SeparatorRegex().Replace(value, string.Empty).ToUpperInvariant();

    public static string NormalizeDocumentType(string? documentType) =>
        string.IsNullOrWhiteSpace(documentType)
            ? string.Empty
            : documentType.Trim().ToUpperInvariant();

    public static bool IsValidPositionTitle(string value) =>
        PositionRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^[\p{L}][\p{L}\p{N} '.-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_./-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentRegex();

    /// <summary>DUI without separators: 8 digits + check digit.</summary>
    [GeneratedRegex(@"^\d{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex DuiRegex();

    /// <summary>NIT without separators: ####-######-###-# collapsed to 14 digits.</summary>
    [GeneratedRegex(@"^\d{14}$", RegexOptions.CultureInvariant)]
    private static partial Regex NitRegex();

    [GeneratedRegex(@"[^A-Za-z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRegex();

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

    public static readonly Error DocumentTypeInvalid = new(
        "LEGAL_REPRESENTATIVE_DOCUMENT_TYPE_INVALID",
        "The document type is not a valid identity document for the company's country.",
        ErrorType.UnprocessableEntity);

    public static readonly Error DocumentNumberFormatInvalid = new(
        "LEGAL_REPRESENTATIVE_DOCUMENT_NUMBER_INVALID",
        "The document number does not match the format of its document type.",
        ErrorType.UnprocessableEntity);

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
            .Must((instance, value) => LegalRepresentativeValidationRules.IsValidDocumentNumber(instance.DocumentType, value))
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

        RuleFor(input => input.EffectiveFrom)
            .NotEmpty();

        RuleFor(input => input.EffectiveTo)
            .Must((input, to) => !to.HasValue || to.Value >= input.EffectiveFrom)
            .WithMessage(LegalRepresentativeErrors.EffectiveDatesInvalid.Message);
    }
}
