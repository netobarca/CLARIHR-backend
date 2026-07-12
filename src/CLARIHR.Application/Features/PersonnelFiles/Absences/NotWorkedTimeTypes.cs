using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.Leave;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.Absences;

// ── Contracts ─────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One type of the company's not-worked-time master (REQ-011 D-18).</summary>
public sealed record NotWorkedTimeTypeResponse(
    Guid NotWorkedTimeTypePublicId,
    string Code,
    string Name,
    bool AppliesToPermission,
    bool UsesWorkSchedule,
    bool CountsHoliday,
    bool CountsSaturday,
    bool CountsRestDay,
    bool CountsSeventhDayPenalty,
    decimal DiscountPercent,
    string? DeductionConceptTypeCode,
    string? IncomeConceptTypeCode,
    bool IsActive,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => NotWorkedTimeTypePublicId;
}

public sealed record NotWorkedTimeTypeInput(
    string Code,
    string Name,
    bool AppliesToPermission,
    bool UsesWorkSchedule,
    bool CountsHoliday,
    bool CountsSaturday,
    bool CountsRestDay,
    bool CountsSeventhDayPenalty,
    decimal DiscountPercent,
    string? DeductionConceptTypeCode,
    string? IncomeConceptTypeCode);

public sealed record GetNotWorkedTimeTypesQuery(Guid CompanyId, bool? IsActive)
    : IQuery<IReadOnlyCollection<NotWorkedTimeTypeResponse>>;

public sealed record GetNotWorkedTimeTypeByIdQuery(Guid CompanyId, Guid NotWorkedTimeTypePublicId)
    : IQuery<NotWorkedTimeTypeResponse>;

public sealed record AddNotWorkedTimeTypeCommand(Guid CompanyId, NotWorkedTimeTypeInput Item)
    : ICommand<NotWorkedTimeTypeResponse>;

public sealed record UpdateNotWorkedTimeTypeCommand(
    Guid CompanyId,
    Guid NotWorkedTimeTypePublicId,
    NotWorkedTimeTypeInput Item,
    Guid ConcurrencyToken) : ICommand<NotWorkedTimeTypeResponse>;

/// <summary>Activate / inactivate — the master has NO DELETE (molde CostCenter): a type that was already used by a
/// record must remain readable, so the removal is logical.</summary>
public sealed record SetNotWorkedTimeTypeActivationCommand(
    Guid CompanyId,
    Guid NotWorkedTimeTypePublicId,
    bool IsActive,
    Guid ConcurrencyToken) : ICommand<NotWorkedTimeTypeResponse>;

/// <summary>Loads the F1 template into the company (idempotent — an existing code is skipped, never overwritten).</summary>
public sealed record LoadNotWorkedTimeTemplateCommand(Guid CompanyId)
    : ICommand<NotWorkedTimeTemplateResultResponse>;

public sealed record NotWorkedTimeTemplateResultResponse(int TypesCreated, int TypesSkipped);

// ── Errors ────────────────────────────────────────────────────────────────────────────────────────────

public static class NotWorkedTimeTypeErrors
{
    public static readonly Error CodeDuplicated = new(
        "NOT_WORKED_TIME_TYPE_CODE_DUPLICATED",
        "Another not-worked-time type already uses this code.",
        ErrorType.Conflict);

    /// <summary>A type that discounts must say WHERE the discount lands, or the money would never reach the payroll
    /// input. (Also enforced by the domain and by a database check constraint.)</summary>
    public static readonly Error DeductionConceptRequired = new(
        "NOT_WORKED_TIME_TYPE_DEDUCTION_CONCEPT_REQUIRED",
        "A type with a discount percent greater than 0 must carry a deduction concept.",
        ErrorType.UnprocessableEntity);

    public static readonly Error NotFound = new(
        "NOT_WORKED_TIME_TYPE_NOT_FOUND",
        "The not-worked-time type does not exist.",
        ErrorType.NotFound);
}

// ── Validators ────────────────────────────────────────────────────────────────────────────────────────

internal sealed class GetNotWorkedTimeTypesQueryValidator : AbstractValidator<GetNotWorkedTimeTypesQuery>
{
    public GetNotWorkedTimeTypesQueryValidator() => RuleFor(query => query.CompanyId).NotEmpty();
}

internal static class NotWorkedTimeTypeInputRules
{
    public static void Apply<T>(AbstractValidator<T> validator, Func<T, NotWorkedTimeTypeInput> selector)
    {
        validator.RuleFor(command => selector(command).Code)
            .NotEmpty().MaximumLength(NotWorkedTimeType.MaxCodeLength);
        validator.RuleFor(command => selector(command).Name)
            .NotEmpty().MaximumLength(NotWorkedTimeType.MaxNameLength);
        validator.RuleFor(command => selector(command).DiscountPercent)
            .InclusiveBetween(0m, 100m);
        validator.RuleFor(command => selector(command).DeductionConceptTypeCode)
            .MaximumLength(NotWorkedTimeType.MaxConceptCodeLength);
        validator.RuleFor(command => selector(command).IncomeConceptTypeCode)
            .MaximumLength(NotWorkedTimeType.MaxConceptCodeLength);
    }
}

internal sealed class GetNotWorkedTimeTypeByIdQueryValidator : AbstractValidator<GetNotWorkedTimeTypeByIdQuery>
{
    public GetNotWorkedTimeTypeByIdQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.NotWorkedTimeTypePublicId).NotEmpty();
    }
}

internal sealed class AddNotWorkedTimeTypeCommandValidator : AbstractValidator<AddNotWorkedTimeTypeCommand>
{
    public AddNotWorkedTimeTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Item).NotNull();
        NotWorkedTimeTypeInputRules.Apply(this, command => command.Item);
    }
}

internal sealed class UpdateNotWorkedTimeTypeCommandValidator : AbstractValidator<UpdateNotWorkedTimeTypeCommand>
{
    public UpdateNotWorkedTimeTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.NotWorkedTimeTypePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Item).NotNull();
        NotWorkedTimeTypeInputRules.Apply(this, command => command.Item);
    }
}

internal sealed class SetNotWorkedTimeTypeActivationCommandValidator
    : AbstractValidator<SetNotWorkedTimeTypeActivationCommand>
{
    public SetNotWorkedTimeTypeActivationCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.NotWorkedTimeTypePublicId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class LoadNotWorkedTimeTemplateCommandValidator : AbstractValidator<LoadNotWorkedTimeTemplateCommand>
{
    public LoadNotWorkedTimeTemplateCommandValidator() => RuleFor(command => command.CompanyId).NotEmpty();
}
