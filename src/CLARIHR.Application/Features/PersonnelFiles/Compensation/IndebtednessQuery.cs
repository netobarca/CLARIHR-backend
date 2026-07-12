using System.Text.Json.Serialization;
using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

using CLARIHR.Application.Features.PersonnelFiles.Compensation;

// ── Contracts ─────────────────────────────────────────────────────────────────────────────────────────

/// <summary>One income line of the base, with the monthly figure it contributes.</summary>
public sealed record IndebtednessBaseLineResponse(
    Guid AssignedPositionPublicId,
    string ConceptTypeCode,
    decimal Value,
    string PayPeriodCode,
    decimal MonthlyValue);

/// <summary>One debt line of the load. A SUSPENDIDO credit travels with <c>isIncludedInLoad = false</c>: it is
/// shown (the operator must see it), but it consumes no capacity (P-12).</summary>
public sealed record IndebtednessLoadLineResponse(
    Guid RecurringDeductionPublicId,
    string TypeCode,
    string? FinancialInstitution,
    string? Reference,
    decimal InstallmentAmount,
    string InstallmentFrequencyCode,
    decimal MonthlyAmount,
    string StatusCode,
    bool IsIncludedInLoad,
    decimal? LimitPercent,
    string? LimitSource);

/// <summary>The employee's indebtedness standing (REQ-010 RF-022).</summary>
public sealed record IndebtednessResponse(
    Guid PersonnelFilePublicId,
    decimal BaseIncome,
    IReadOnlyCollection<IndebtednessBaseLineResponse> BaseBreakdown,
    decimal CurrentLoad,
    IReadOnlyCollection<IndebtednessLoadLineResponse> LoadBreakdown,
    decimal CurrentPercent,
    decimal? GlobalLimitPercent,
    IReadOnlyDictionary<string, decimal> LimitsByType,
    /// <summary>DENTRO · EXCEDIDO · <b>SIN_CONTROL</b> (no parameters configured — a legitimate state, not an error).</summary>
    string Status,
    IReadOnlyCollection<IndebtednessOverrideResponse> Overrides)
{
    [JsonIgnore]
    public Guid Id => PersonnelFilePublicId;
}

/// <summary>The deduction being simulated. <c>TypeCode</c> selects the ceiling that would apply to it.</summary>
public sealed record SimulatedDeductionInput(
    decimal Amount,
    string PayPeriodCode,
    string? TypeCode);

/// <summary>The outcome of a simulation (RF-023): the same standing, plus what WOULD happen.</summary>
public sealed record IndebtednessSimulationResponse(
    decimal BaseIncome,
    decimal CurrentLoad,
    decimal CurrentPercent,
    decimal AdditionalMonthlyDeduction,
    decimal SimulatedPercent,
    decimal? LimitPercent,
    string? LimitSource,
    bool WouldExceed,
    string Status);

public sealed record GetIndebtednessQuery(Guid PersonnelFileId) : IQuery<IndebtednessResponse>;

/// <summary>
/// A simulation (RF-023). It is an <b>IQuery</b>, not a command — the levantamiento is literal: <i>"solo simulación
/// y no debe afectar la planilla"</i>. The HTTP verb is POST only because the input has a body.
/// <para><c>BaseIncomeOverride</c> is the "ingreso digitado" of the levantamiento: when omitted, the derived base
/// is used.</para>
/// </summary>
public sealed record SimulateIndebtednessQuery(
    Guid PersonnelFileId,
    decimal? BaseIncomeOverride,
    SimulatedDeductionInput AdditionalDeduction) : IQuery<IndebtednessSimulationResponse>;

// ── Validators ────────────────────────────────────────────────────────────────────────────────────────

internal sealed class GetIndebtednessQueryValidator : AbstractValidator<GetIndebtednessQuery>
{
    public GetIndebtednessQueryValidator() => RuleFor(query => query.PersonnelFileId).NotEmpty();
}

internal sealed class SimulateIndebtednessQueryValidator : AbstractValidator<SimulateIndebtednessQuery>
{
    public SimulateIndebtednessQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.BaseIncomeOverride)
            .GreaterThanOrEqualTo(0m)
            .When(query => query.BaseIncomeOverride.HasValue);
        RuleFor(query => query.AdditionalDeduction).NotNull();
        RuleFor(query => query.AdditionalDeduction.Amount).GreaterThan(0m);
        RuleFor(query => query.AdditionalDeduction.PayPeriodCode).NotEmpty().MaximumLength(40);
    }
}
