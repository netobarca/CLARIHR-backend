using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.Payroll;

/// <summary>One persisted line of a run as it travels on the wire (drill — REQ-013 RF-002 · REQ-015 RF-002/003).</summary>
public sealed record PayrollRunLineResponse(
    Guid Id,
    Guid EmployeePublicId,
    string EmployeeName,
    string? EmployeeCode,
    Guid? AssignedPositionPublicId,
    string? CostCenterName,
    string ConceptCode,
    string ConceptName,
    string LineClass,
    decimal? Units,
    decimal? BaseAmount,
    decimal CalculatedAmount,
    decimal? OverrideAmount,
    string? OverrideNote,
    decimal FinalAmount,
    bool IsIncluded,
    string? SourceModule,
    Guid? SourceReferencePublicId,
    string CurrencyCode,
    IReadOnlyList<string> WarningCodes,
    int SortOrder);

public sealed record PayrollRunEmployeeLinesResponse(
    Guid PayrollRunId,
    Guid EmployeePublicId,
    string EmployeeName,
    decimal TotalIncome,
    decimal TotalDeductions,
    decimal TotalNet,
    IReadOnlyList<PayrollRunLineResponse> Lines);

public sealed record GetPayrollRunByIdQuery(Guid CompanyId, Guid PayrollRunId) : IQuery<PayrollRunResponse>;

public sealed record GetPayrollRunEmployeeLinesQuery(Guid CompanyId, Guid PayrollRunId, Guid PersonnelFilePublicId)
    : IQuery<PayrollRunEmployeeLinesResponse>;

/// <summary>
/// Review adjustment of one line (only while GENERADA): an audited override (amount + mandatory note;
/// null clears it) and/or the inclusion flag. EXCLUDING a pool line reverts its MOTOR application (the
/// source record is released and re-appears in the next run — REQ-014 RF-007); re-including re-applies it.
/// </summary>
public sealed record AdjustPayrollRunLineCommand(
    Guid CompanyId,
    Guid PayrollRunId,
    Guid LineId,
    bool OverrideSupplied,
    decimal? OverrideAmount,
    string? OverrideNote,
    bool? IsIncluded,
    Guid ConcurrencyToken)
    : ICommand<PayrollRunResponse>;

/// <summary>Selective recalculation (§3.6): reverts + recomputes ONLY the given employees' lines, keeping overrides.</summary>
public sealed record RecalculatePayrollRunCommand(
    Guid CompanyId,
    Guid PayrollRunId,
    IReadOnlyCollection<Guid> EmployeeIds,
    Guid ConcurrencyToken)
    : ICommand<PayrollRunResponse>;

/// <summary>Full regeneration (§3.6): reverts every MOTOR application and re-runs; adjustments are discarded.</summary>
public sealed record RegeneratePayrollRunCommand(
    Guid CompanyId,
    Guid PayrollRunId,
    Guid ConcurrencyToken)
    : ICommand<PayrollRunResponse>;

public sealed record AuthorizePayrollRunCommand(Guid CompanyId, Guid PayrollRunId, Guid ConcurrencyToken)
    : ICommand<PayrollRunResponse>;

public sealed record ReturnPayrollRunCommand(Guid CompanyId, Guid PayrollRunId, string Reason, Guid ConcurrencyToken)
    : ICommand<PayrollRunResponse>;

public sealed record ClosePayrollRunCommand(Guid CompanyId, Guid PayrollRunId, Guid ConcurrencyToken)
    : ICommand<PayrollRunResponse>;

public sealed record AnnulPayrollRunCommand(Guid CompanyId, Guid PayrollRunId, string Reason, Guid ConcurrencyToken)
    : ICommand<PayrollRunResponse>;

public static class PayrollRunReviewErrors
{
    /// <summary>Separation of duties + double anti-self: the generator never authorizes their own run.</summary>
    public static readonly Error SelfAuthorizationForbidden = new(
        "PAYROLL_RUN_SELF_AUTHORIZATION_FORBIDDEN",
        "The user who generated the payroll run cannot authorize it.",
        ErrorType.Forbidden);

    public static readonly Error ReturnReasonRequired = new(
        "PAYROLL_RUN_RETURN_REASON_REQUIRED",
        "A reason is required to return the payroll run.",
        ErrorType.UnprocessableEntity);

    public static readonly Error AnnulmentReasonRequired = new(
        "PAYROLL_RUN_ANNULMENT_REASON_REQUIRED",
        "A reason is required to annul the payroll run.",
        ErrorType.UnprocessableEntity);

    public static readonly Error LineNotFound = new(
        "PAYROLL_RUN_LINE_NOT_FOUND",
        "The payroll run line could not be found.",
        ErrorType.NotFound);

    /// <summary>Law lines (ISSS/AFP/Renta/patronales) admit no override nor exclusion — the law is never edited.</summary>
    public static readonly Error LineNotAdjustable = new(
        "PAYROLL_RUN_LINE_NOT_ADJUSTABLE",
        "The payroll run line does not admit adjustments.",
        ErrorType.UnprocessableEntity);
}

internal sealed class GetPayrollRunByIdQueryValidator : AbstractValidator<GetPayrollRunByIdQuery>
{
    public GetPayrollRunByIdQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PayrollRunId).NotEmpty();
    }
}

internal sealed class GetPayrollRunEmployeeLinesQueryValidator : AbstractValidator<GetPayrollRunEmployeeLinesQuery>
{
    public GetPayrollRunEmployeeLinesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PayrollRunId).NotEmpty();
        RuleFor(query => query.PersonnelFilePublicId).NotEmpty();
    }
}

internal sealed class AdjustPayrollRunLineCommandValidator : AbstractValidator<AdjustPayrollRunLineCommand>
{
    public AdjustPayrollRunLineCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollRunId).NotEmpty();
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command)
            .Must(command => command.OverrideSupplied || command.IsIncluded.HasValue)
            .WithMessage("The adjustment must supply an override or an inclusion flag.");
        RuleFor(command => command.OverrideNote)
            .NotEmpty()
            .When(command => command is { OverrideSupplied: true, OverrideAmount: not null })
            .WithMessage("An override requires a note.");
        RuleFor(command => command.OverrideNote).MaximumLength(500);
        RuleFor(command => command.OverrideAmount)
            .GreaterThanOrEqualTo(0m)
            .When(command => command.OverrideAmount.HasValue);
    }
}

internal sealed class RecalculatePayrollRunCommandValidator : AbstractValidator<RecalculatePayrollRunCommand>
{
    public RecalculatePayrollRunCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollRunId).NotEmpty();
        RuleFor(command => command.EmployeeIds).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class RegeneratePayrollRunCommandValidator : AbstractValidator<RegeneratePayrollRunCommand>
{
    public RegeneratePayrollRunCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollRunId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AuthorizePayrollRunCommandValidator : AbstractValidator<AuthorizePayrollRunCommand>
{
    public AuthorizePayrollRunCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollRunId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ReturnPayrollRunCommandValidator : AbstractValidator<ReturnPayrollRunCommand>
{
    public ReturnPayrollRunCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollRunId).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(500);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ClosePayrollRunCommandValidator : AbstractValidator<ClosePayrollRunCommand>
{
    public ClosePayrollRunCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollRunId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class AnnulPayrollRunCommandValidator : AbstractValidator<AnnulPayrollRunCommand>
{
    public AnnulPayrollRunCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollRunId).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(500);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}
