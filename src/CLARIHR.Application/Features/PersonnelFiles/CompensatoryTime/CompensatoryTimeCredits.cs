using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Resolved snapshot of an ACTIVE compensatory-time type: its internal id plus the code/name/operation/factor
/// captured on the credit at registration (RN-02/RN-04). Returned by the repository's type resolution.
/// </summary>
public sealed record CompensatoryTimeTypeRef(
    long InternalId,
    string Code,
    string Name,
    string OperationCode,
    decimal CreditFactor);

/// <summary>
/// A compensatory-time credit ("acreditación de tiempo compensatorio", REQ-002 D-02/D-20): a declarative record
/// of hours worked outside the regular schedule that credit hours into the employee fund. The credited hours are
/// <c>Round2(hoursWorked × factor)</c> (snapshot of the type factor) unless HR entered a manual adjustment
/// (<see cref="IsOverridden"/> — the override note is then mandatory). The read gate is
/// <c>ViewCompensatoryTime</c> OR the owner employee.
/// </summary>
public sealed record PersonnelFileCompensatoryTimeCreditResponse(
    Guid CompensatoryTimeCreditPublicId,
    Guid CompensatoryTimeTypePublicId,
    string CompensatoryTimeTypeCode,
    string TypeNameSnapshot,
    DateOnly WorkDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal HoursWorked,
    decimal FactorApplied,
    decimal HoursCredited,
    bool IsOverridden,
    string? OverrideNote,
    string WorkDetail,
    string AuthorizedByText,
    Guid? AuthorizerFilePublicId,
    Guid? AssignedPositionPublicId,
    Guid? OvertimeRecordPublicId,
    string StatusCode,
    string? AnnulmentReason,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc)
{
    [JsonIgnore]
    public Guid Id => CompensatoryTimeCreditPublicId;
}

/// <summary>
/// Business fields for registering or editing a compensatory-time credit. The type travels as a public id and is
/// resolved to its internal id/snapshot by the handler (422 when inactive/foreign or not a crediting operation).
/// The credited hours are computed from <see cref="HoursWorked"/> × factor unless <see cref="HoursCreditedOverride"/>
/// is supplied, in which case <see cref="OverrideNote"/> is mandatory (RN-02).
/// </summary>
public sealed record CompensatoryTimeCreditInput(
    Guid CompensatoryTimeTypePublicId,
    DateOnly WorkDate,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal HoursWorked,
    decimal? HoursCreditedOverride,
    string? OverrideNote,
    string WorkDetail,
    string AuthorizedByText,
    Guid? AssignedPositionPublicId,
    Guid? OvertimeRecordPublicId,
    string? Notes);

public sealed record AddCompensatoryTimeCreditCommand(
    Guid PersonnelFileId,
    CompensatoryTimeCreditInput Item,
    Guid? AuthorizationFilePublicId,
    Guid? DocumentTypeCatalogItemPublicId,
    string? DocumentObservations)
    : ICommand<PersonnelFileCompensatoryTimeCreditResponse>;

public sealed record UpdateCompensatoryTimeCreditCommand(
    Guid PersonnelFileId,
    Guid CompensatoryTimeCreditPublicId,
    CompensatoryTimeCreditInput Item,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCompensatoryTimeCreditResponse>;

public sealed record AnnulCompensatoryTimeCreditCommand(
    Guid PersonnelFileId,
    Guid CompensatoryTimeCreditPublicId,
    string Reason,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileCompensatoryTimeCreditResponse>;

public sealed record GetCompensatoryTimeCreditsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileCompensatoryTimeCreditResponse>>;

public sealed record GetCompensatoryTimeCreditByIdQuery(Guid PersonnelFileId, Guid CompensatoryTimeCreditPublicId)
    : IQuery<PersonnelFileCompensatoryTimeCreditResponse>;

// ── Validators ─────────────────────────────────────────────────────────────────────────────────

internal sealed class CompensatoryTimeCreditInputValidator : AbstractValidator<CompensatoryTimeCreditInput>
{
    public CompensatoryTimeCreditInputValidator()
    {
        RuleFor(input => input.CompensatoryTimeTypePublicId).NotEmpty();
        RuleFor(input => input.WorkDate).NotEmpty();
        RuleFor(input => input.HoursWorked).GreaterThan(0m).LessThanOrEqualTo(24m);
        RuleFor(input => input.HoursCreditedOverride)
            .GreaterThan(0m)
            .When(input => input.HoursCreditedOverride.HasValue);
        RuleFor(input => input.OverrideNote).MaximumLength(500);
        RuleFor(input => input.WorkDetail).NotEmpty().MaximumLength(500);
        RuleFor(input => input.AuthorizedByText).NotEmpty().MaximumLength(200);
        RuleFor(input => input.Notes).MaximumLength(1000);
    }
}

internal sealed class AddCompensatoryTimeCreditCommandValidator : AbstractValidator<AddCompensatoryTimeCreditCommand>
{
    public AddCompensatoryTimeCreditCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new CompensatoryTimeCreditInputValidator());
        RuleFor(command => command.DocumentObservations).MaximumLength(2000);
    }
}

internal sealed class UpdateCompensatoryTimeCreditCommandValidator : AbstractValidator<UpdateCompensatoryTimeCreditCommand>
{
    public UpdateCompensatoryTimeCreditCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.CompensatoryTimeCreditPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull().SetValidator(new CompensatoryTimeCreditInputValidator());
    }
}

internal sealed class AnnulCompensatoryTimeCreditCommandValidator : AbstractValidator<AnnulCompensatoryTimeCreditCommand>
{
    public AnnulCompensatoryTimeCreditCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.CompensatoryTimeCreditPublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class GetCompensatoryTimeCreditsQueryValidator : AbstractValidator<GetCompensatoryTimeCreditsQuery>
{
    public GetCompensatoryTimeCreditsQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class GetCompensatoryTimeCreditByIdQueryValidator : AbstractValidator<GetCompensatoryTimeCreditByIdQuery>
{
    public GetCompensatoryTimeCreditByIdQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.CompensatoryTimeCreditPublicId).NotEmpty();
    }
}

/// <summary>
/// Dedicated errors for compensatory-time credits (REQ-002 §5). Each code requires an EN + ES resource entry
/// (parity: <c>BackendMessageLocalizationTests</c>). Field-level validation lives in the validators (400).
/// </summary>
internal static class CompensatoryTimeCreditErrors
{
    public static readonly Error TypeInvalid = new(
        "COMPENSATORY_TIME_TYPE_INVALID",
        "The compensatory-time type does not exist or is inactive for the company.", ErrorType.UnprocessableEntity);

    public static readonly Error TypeOperationMismatch = new(
        "COMPENSATORY_TIME_TYPE_OPERATION_MISMATCH",
        "The compensatory-time type does not allow this operation (a credit requires an ACREDITA or AMBAS type).", ErrorType.UnprocessableEntity);

    public static readonly Error WorkDateInFuture = new(
        "COMPENSATORY_TIME_WORK_DATE_IN_FUTURE",
        "The worked date cannot be later than today.", ErrorType.UnprocessableEntity);

    public static readonly Error TimeRangeInvalid = new(
        "COMPENSATORY_TIME_TIME_RANGE_INVALID",
        "The worked-time range is incoherent (the end time must be later than the start time).", ErrorType.UnprocessableEntity);

    public static readonly Error DocumentRequired = new(
        "COMPENSATORY_TIME_DOCUMENT_REQUIRED",
        "An authorization document is required to register the compensatory-time credit.", ErrorType.UnprocessableEntity);

    public static readonly Error DocumentPurposeInvalid = new(
        "COMPENSATORY_TIME_DOCUMENT_PURPOSE_INVALID",
        "The referenced file was not uploaded with the compensatory-time-document purpose.", ErrorType.UnprocessableEntity);

    public static readonly Error OverrideNoteRequired = new(
        "COMPENSATORY_TIME_OVERRIDE_NOTE_REQUIRED",
        "A note is required when the credited hours are manually adjusted.", ErrorType.UnprocessableEntity);

    public static readonly Error MaxBalanceExceeded = new(
        "COMPENSATORY_TIME_MAX_BALANCE_EXCEEDED",
        "The credit would take the fund balance over the configured maximum.", ErrorType.UnprocessableEntity);

    public static readonly Error BalanceWouldGoNegative = new(
        "COMPENSATORY_TIME_BALANCE_WOULD_GO_NEGATIVE",
        "Editing or annulling this credit would drive the fund balance negative (there are debits against it).", ErrorType.UnprocessableEntity);

    public static readonly Error AnnulmentReasonRequired = new(
        "COMPENSATORY_TIME_ANNULMENT_REASON_REQUIRED",
        "A reason is required to annul a compensatory-time credit.", ErrorType.UnprocessableEntity);

    public static readonly Error StateRuleViolation = new(
        "COMPENSATORY_TIME_STATE_RULE_VIOLATION",
        "The compensatory-time credit is not in a state that allows this operation.", ErrorType.UnprocessableEntity);
}
