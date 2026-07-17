using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Payroll.Common;
using FluentValidation;

namespace CLARIHR.Application.Features.Payroll;

/// <summary>Run header as it travels on the wire (the per-employee drill arrives in PR-6).</summary>
public sealed record PayrollRunResponse(
    Guid Id,
    Guid PayrollDefinitionPublicId,
    Guid PayrollPeriodPublicId,
    string PayrollDefinitionCode,
    string PayrollDefinitionName,
    string PayrollTypeCode,
    string PeriodLabel,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    DateOnly? PaymentDate,
    string CurrencyCode,
    string StatusCode,
    Guid GeneratedByUserPublicId,
    DateTime GeneratedUtc,
    int RegeneratedCount,
    int EmployeeCount,
    decimal TotalIncome,
    decimal TotalDeductions,
    decimal TotalEmployerCost,
    decimal TotalNet,
    IReadOnlyList<PayrollRunWarningResponse> Warnings,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PayrollRunWarningResponse(string Code, Guid? PersonnelFilePublicId, string? Context);

/// <summary>
/// Generates the run of a Nómina × period (REQ-012 §3.4): population → pure engine → persisted lines →
/// pools applied with origin MOTOR. Optional <paramref name="EmployeeIds"/> restricts the population
/// (RF-008 — REQ-014 P-01: the selection is exercised on the GENERADA run afterwards).
/// </summary>
public sealed record GeneratePayrollRunCommand(
    Guid CompanyId,
    Guid PayrollDefinitionPublicId,
    Guid PayrollPeriodPublicId,
    IReadOnlyCollection<Guid>? EmployeeIds)
    : ICommand<PayrollRunResponse>;

/// <summary>Same inputs WITHOUT writing (§3.4): the operator's preview, including the REQ-014 lag inventory.</summary>
public sealed record PreflightPayrollRunCommand(
    Guid CompanyId,
    Guid PayrollDefinitionPublicId,
    Guid PayrollPeriodPublicId,
    IReadOnlyCollection<Guid>? EmployeeIds)
    : ICommand<PayrollRunPreflightResponse>;

/// <summary>Pre-flight summary: the population, the per-module input counts and the stable warnings.</summary>
public sealed record PayrollRunPreflightResponse(
    int EmployeeCount,
    int SalaryLineCandidates,
    int PoolIncomeCandidates,
    int OvertimeCandidates,
    int PoolDeductionCandidates,
    int NotWorkedTimeInputs,
    int DisciplinaryInputs,
    int IncapacityInputs,
    int CarryoverInputs,
    decimal ProjectedTotalIncome,
    decimal ProjectedTotalDeductions,
    decimal ProjectedTotalNet,
    IReadOnlyList<PayrollRunWarningResponse> Warnings);

public static class PayrollRunErrors
{
    public static readonly Error PayrollRunNotFound = new(
        "PAYROLL_RUN_NOT_FOUND",
        "The payroll run could not be found.",
        ErrorType.NotFound);

    /// <summary>The Nómina × period already has an ACTIVE run (annul it to regenerate the slot).</summary>
    public static readonly Error AlreadyActive = new(
        "PAYROLL_RUN_ALREADY_ACTIVE",
        "The payroll definition already has an active run for this period.",
        ErrorType.Conflict);

    /// <summary>A pool record changed between the scan and the apply — full rollback (§3.5).</summary>
    public static readonly Error PoolConflict = new(
        "PAYROLL_RUN_POOL_CONFLICT",
        "A payroll input changed while generating the run. Retry the generation.",
        ErrorType.Conflict);

    /// <summary>Generation needs the Nómina, the period and their coherence (period hangs from the Nómina).</summary>
    public static readonly Error InputInvalid = new(
        "PAYROLL_RUN_INPUT_INVALID",
        "The payroll definition or period is not valid for generation.",
        ErrorType.UnprocessableEntity);

    public static readonly Error StateRuleViolation = new(
        "PAYROLL_RUN_STATE_RULE_VIOLATION",
        "The payroll run status does not allow the requested change.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    /// <summary>
    /// REQ-016 Gate A (ratified P-03): while the tenant's payroll compliance gates are enabled
    /// (CompanyPreference.PayrollComplianceGatesEnabled), a company with no legal profile cannot
    /// generate any payroll run.
    /// </summary>
    public static readonly Error MissingLegalProfile = new(
        "PAYROLL_RUN_MISSING_LEGAL_PROFILE",
        "The company does not have a legal profile configured; payroll cannot be generated until it does.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch("PAYROLL_RUNS", action);
}

internal sealed class GeneratePayrollRunCommandValidator : AbstractValidator<GeneratePayrollRunCommand>
{
    public GeneratePayrollRunCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollDefinitionPublicId).NotEmpty();
        RuleFor(command => command.PayrollPeriodPublicId).NotEmpty();
        RuleFor(command => command.EmployeeIds)
            .Must(ids => ids is null || ids.Count > 0)
            .WithMessage("Employee ids must be omitted or non-empty.");
    }
}

internal sealed class PreflightPayrollRunCommandValidator : AbstractValidator<PreflightPayrollRunCommand>
{
    public PreflightPayrollRunCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollDefinitionPublicId).NotEmpty();
        RuleFor(command => command.PayrollPeriodPublicId).NotEmpty();
        RuleFor(command => command.EmployeeIds)
            .Must(ids => ids is null || ids.Count > 0)
            .WithMessage("Employee ids must be omitted or non-empty.");
    }
}
