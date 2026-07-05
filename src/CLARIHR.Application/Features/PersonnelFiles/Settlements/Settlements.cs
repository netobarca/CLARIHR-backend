using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Read model: the settlement detail in its five sections (lines grouped by ConceptClass on the
// client), the parameters/derived snapshot, the totals summary and the non-blocking warnings
// (pre-development clarification №4: warnings ride the response, never ProblemDetails).
// ─────────────────────────────────────────────────────────────────────────────────────────────

public sealed record SettlementWarningResponse(string Code, string? ConceptCode);

public sealed record PersonnelFileSettlementLineResponse(
    Guid Id,
    SettlementConceptClass ConceptClass,
    string ConceptCode,
    string ConceptName,
    string? Description,
    bool IsSystemCalculated,
    decimal? CalculationBase,
    decimal? UnitsOrDays,
    bool UnitsOverridden,
    decimal CalculatedAmount,
    decimal ExemptAmount,
    decimal TaxableExcessAmount,
    decimal? OverrideAmount,
    string? OverrideReason,
    decimal FinalAmount,
    bool IsIncluded,
    bool IsZeroByLaw,
    string? ZeroReasonCode,
    string? CalculationDetail,
    string? CounterpartyName,
    int SortOrder);

public sealed record PersonnelFileSettlementResponse(
    Guid Id,
    SettlementKind Kind,
    string? StatusCode,
    Guid? RetirementRequestPublicId,
    Guid AssignedPositionPublicId,
    string? PositionName,
    DateTime PlazaStartDate,
    Guid? CostCenterPublicId,
    string? CostCenterName,
    DateTime RetirementDate,
    string RetirementCategoryCode,
    string? RetirementCategoryName,
    string RetirementReasonCode,
    string? RetirementReasonName,
    Guid RequesterFilePublicId,
    string RequesterName,
    DateTime RequestDate,
    string? Notes,
    decimal MinimumMonthlyWage,
    decimal IndemnityCapMultiplier,
    decimal ResignationCapMultiplier,
    decimal VacationDays,
    decimal VacationPremiumPercent,
    decimal AguinaldoDays,
    decimal ResignationBenefitDays,
    int ResignationMinimumServiceYears,
    decimal AguinaldoExemptionMultiplier,
    int MonthDivisorDays,
    int YearDivisorDays,
    decimal MonthlyBaseSalary,
    int SeniorityYears,
    int SeniorityDays,
    decimal CappedMonthlySalaryIndemnity,
    decimal CappedMonthlySalaryResignation,
    decimal TotalIncomes,
    decimal TotalDeductions,
    decimal NetPay,
    decimal TotalEmployerCharges,
    decimal ProvisionTotal,
    string CurrencyCode,
    Guid? IssuedByUserId,
    DateTime? IssuedAtUtc,
    Guid? AnnulledByUserId,
    DateTime? AnnulledAtUtc,
    string? AnnulmentReason,
    IReadOnlyCollection<PersonnelFileSettlementLineResponse> Lines,
    IReadOnlyCollection<SettlementWarningResponse> Warnings,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

// ── Inputs ───────────────────────────────────────────────────────────────────────────────────

/// <summary>Legal parameters as edited per settlement (RF-011); every value snapshots into the record.</summary>
public sealed record SettlementParametersInputModel(
    decimal MinimumMonthlyWage,
    decimal IndemnityCapMultiplier,
    decimal ResignationCapMultiplier,
    decimal VacationDays,
    decimal VacationPremiumPercent,
    decimal AguinaldoDays,
    decimal ResignationBenefitDays,
    int ResignationMinimumServiceYears,
    decimal AguinaldoExemptionMultiplier,
    int MonthDivisorDays,
    int YearDivisorDays);

/// <summary>Scenario creation input (D-05): an active plaza, an estimated date and a hypothetical motive.</summary>
public sealed record SettlementScenarioInput(
    Guid AssignedPositionPublicId,
    DateTime EstimatedRetirementDate,
    string RetirementCategoryCode,
    string RetirementReasonCode,
    DateTime RequestDate,
    Guid? RequesterFilePublicId,
    string? Notes,
    // Override when the employee "ficha" has no minimum wage yet (RN-001.7).
    decimal? MinimumMonthlyWage);

// ── Commands / queries ───────────────────────────────────────────────────────────────────────

public sealed record AddSettlementScenarioCommand(Guid PersonnelFileId, SettlementScenarioInput Item)
    : ICommand<PersonnelFileSettlementResponse>;

/// <summary>
/// Header + parameters edit (and, on scenarios, the hypothetical assumptions). Recalculates on save.
/// </summary>
public sealed record UpdateSettlementCommand(
    Guid PersonnelFileId,
    Guid SettlementId,
    Guid ConcurrencyToken,
    Guid? RequesterFilePublicId,
    DateTime RequestDate,
    string? Notes,
    DateTime? EstimatedRetirementDate,
    string? RetirementCategoryCode,
    string? RetirementReasonCode,
    SettlementParametersInputModel Parameters)
    : ICommand<PersonnelFileSettlementResponse>;

/// <summary>
/// One-line adjustment (D-14/RN-002.2): include/exclude, fix or release the days input, set or clear the
/// audited override, or edit a manual line's description/amount. Recalculates on save.
/// </summary>
public sealed record UpdateSettlementLineCommand(
    Guid PersonnelFileId,
    Guid SettlementId,
    Guid LineId,
    Guid ConcurrencyToken,
    bool? IsIncluded,
    decimal? UnitsOrDays,
    bool ClearUnitsOverride,
    decimal? OverrideAmount,
    string? OverrideReason,
    bool ClearOverride,
    string? Description,
    decimal? ManualAmount)
    : ICommand<PersonnelFileSettlementResponse>;

public sealed record AddSettlementManualLineCommand(
    Guid PersonnelFileId,
    Guid SettlementId,
    Guid ConcurrencyToken,
    string ConceptCode,
    string Description,
    decimal Amount)
    : ICommand<PersonnelFileSettlementResponse>;

public sealed record RemoveSettlementLineCommand(
    Guid PersonnelFileId,
    Guid SettlementId,
    Guid LineId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSettlementResponse>;

/// <summary>Explicit regenerate (pre-development clarification №2): re-reads the config and rebuilds the suggested lines.</summary>
public sealed record RegenerateSettlementLinesCommand(
    Guid PersonnelFileId,
    Guid SettlementId,
    Guid ConcurrencyToken)
    : ICommand<PersonnelFileSettlementResponse>;

/// <summary>Soft-delete of a SCENARIO (a real settlement is annulled, never deleted).</summary>
public sealed record DeleteSettlementScenarioCommand(Guid PersonnelFileId, Guid SettlementId, Guid ConcurrencyToken)
    : ICommand<bool>;

public sealed record GetSettlementQuery(Guid PersonnelFileId, Guid SettlementId)
    : IQuery<PersonnelFileSettlementResponse?>;

public sealed record GetSettlementsQuery(Guid PersonnelFileId)
    : IQuery<IReadOnlyCollection<PersonnelFileSettlementResponse>>;

// ── Errors (bilingual resx entries keyed by code) ────────────────────────────────────────────

internal static class SettlementErrors
{
    public static readonly Error NotFound = new(
        "SETTLEMENT_NOT_FOUND",
        "The settlement does not exist for this personnel file.",
        ErrorType.NotFound);

    public static readonly Error LineNotFound = new(
        "SETTLEMENT_LINE_NOT_FOUND",
        "The settlement line does not exist.",
        ErrorType.NotFound);

    public static readonly Error StateRuleViolation = new(
        "SETTLEMENT_STATE_RULE_VIOLATION",
        "The operation is not valid for the settlement's current state (only a BORRADOR settlement or a scenario is editable; a real settlement is annulled, never deleted).",
        ErrorType.UnprocessableEntity);

    public static readonly Error ScenarioEmployeeRetired = new(
        "SETTLEMENT_SCENARIO_EMPLOYEE_RETIRED",
        "A scenario simulates over an active employee; a retired employee gets a real settlement anchored to the executed retirement.",
        ErrorType.UnprocessableEntity);

    public static readonly Error PositionInvalid = new(
        "SETTLEMENT_POSITION_INVALID",
        "The assigned position (plaza) does not belong to the personnel file or is not valid for this settlement kind.",
        ErrorType.UnprocessableEntity);

    public static readonly Error BaseSalaryMissing = new(
        "SETTLEMENT_BASE_SALARY_MISSING",
        "The plaza has no active negotiated base salary (SALARIO_BASE); configure the compensation before settling.",
        ErrorType.UnprocessableEntity);

    public static readonly Error MinimumWageMissing = new(
        "SETTLEMENT_MINIMUM_WAGE_MISSING",
        "The employee's record has no applicable minimum monthly wage; register it on the employment information or supply it in the settlement.",
        ErrorType.UnprocessableEntity);

    public static readonly Error RequesterNotHr = new(
        "SETTLEMENT_REQUESTER_NOT_HR",
        "The settlement requester can only be a member of HR (the registering manager by default).",
        ErrorType.UnprocessableEntity);

    public static readonly Error SelfActionForbidden = new(
        "SETTLEMENT_SELF_ACTION_FORBIDDEN",
        "The subject employee cannot manage their own settlement.",
        ErrorType.Forbidden);

    public static readonly Error DateIncoherent = new(
        "SETTLEMENT_DATE_INCOHERENT",
        "The request date cannot be in the future and the retirement date cannot precede the plaza start date.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ConceptInvalid = new(
        "SETTLEMENT_CONCEPT_INVALID",
        "The settlement concept is not an active manual concept of the catalog for this section.",
        ErrorType.UnprocessableEntity);

    public static readonly Error OverrideNoteRequired = new(
        "SETTLEMENT_OVERRIDE_NOTE_REQUIRED",
        "A manual override requires a reason note.",
        ErrorType.UnprocessableEntity);
}

// ── Validators ───────────────────────────────────────────────────────────────────────────────

internal sealed class SettlementParametersInputModelValidator : AbstractValidator<SettlementParametersInputModel>
{
    public SettlementParametersInputModelValidator()
    {
        RuleFor(model => model.MinimumMonthlyWage).GreaterThan(0);
        RuleFor(model => model.IndemnityCapMultiplier).GreaterThan(0);
        RuleFor(model => model.ResignationCapMultiplier).GreaterThan(0);
        RuleFor(model => model.VacationDays).GreaterThanOrEqualTo(0);
        RuleFor(model => model.VacationPremiumPercent).GreaterThanOrEqualTo(0);
        RuleFor(model => model.AguinaldoDays).GreaterThanOrEqualTo(0);
        RuleFor(model => model.ResignationBenefitDays).GreaterThanOrEqualTo(0);
        RuleFor(model => model.ResignationMinimumServiceYears).GreaterThanOrEqualTo(0);
        RuleFor(model => model.AguinaldoExemptionMultiplier).GreaterThanOrEqualTo(0);
        RuleFor(model => model.MonthDivisorDays).GreaterThan(0);
        RuleFor(model => model.YearDivisorDays).GreaterThan(0);
    }
}

internal sealed class AddSettlementScenarioCommandValidator : AbstractValidator<AddSettlementScenarioCommand>
{
    public AddSettlementScenarioCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.Item).NotNull();
        RuleFor(command => command.Item.AssignedPositionPublicId).NotEmpty();
        RuleFor(command => command.Item.RetirementCategoryCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Item.RetirementReasonCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Item.Notes).MaximumLength(2000);
        RuleFor(command => command.Item.MinimumMonthlyWage)
            .GreaterThan(0)
            .When(command => command.Item.MinimumMonthlyWage.HasValue);
    }
}

internal sealed class UpdateSettlementCommandValidator : AbstractValidator<UpdateSettlementCommand>
{
    public UpdateSettlementCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.SettlementId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Notes).MaximumLength(2000);
        RuleFor(command => command.RetirementCategoryCode).MaximumLength(80);
        RuleFor(command => command.RetirementReasonCode).MaximumLength(80);
        RuleFor(command => command.Parameters).NotNull().SetValidator(new SettlementParametersInputModelValidator());
    }
}

internal sealed class UpdateSettlementLineCommandValidator : AbstractValidator<UpdateSettlementLineCommand>
{
    public UpdateSettlementLineCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.SettlementId).NotEmpty();
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.UnitsOrDays).GreaterThanOrEqualTo(0).When(command => command.UnitsOrDays.HasValue);
        RuleFor(command => command.OverrideReason).MaximumLength(500);
        RuleFor(command => command.Description).MaximumLength(300);
        RuleFor(command => command.ManualAmount).GreaterThanOrEqualTo(0).When(command => command.ManualAmount.HasValue);
    }
}

internal sealed class AddSettlementManualLineCommandValidator : AbstractValidator<AddSettlementManualLineCommand>
{
    public AddSettlementManualLineCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.SettlementId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.ConceptCode).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(300);
        RuleFor(command => command.Amount).GreaterThanOrEqualTo(0);
    }
}

internal sealed class RemoveSettlementLineCommandValidator : AbstractValidator<RemoveSettlementLineCommand>
{
    public RemoveSettlementLineCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.SettlementId).NotEmpty();
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class RegenerateSettlementLinesCommandValidator : AbstractValidator<RegenerateSettlementLinesCommand>
{
    public RegenerateSettlementLinesCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.SettlementId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class DeleteSettlementScenarioCommandValidator : AbstractValidator<DeleteSettlementScenarioCommand>
{
    public DeleteSettlementScenarioCommandValidator()
    {
        RuleFor(command => command.PersonnelFileId).NotEmpty();
        RuleFor(command => command.SettlementId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetSettlementQueryValidator : AbstractValidator<GetSettlementQuery>
{
    public GetSettlementQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
        RuleFor(query => query.SettlementId).NotEmpty();
    }
}

internal sealed class GetSettlementsQueryValidator : AbstractValidator<GetSettlementsQuery>
{
    public GetSettlementsQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

// ── Mapper + persisted-state warnings ────────────────────────────────────────────────────────

internal static class SettlementResponseMapper
{
    public static PersonnelFileSettlementResponse Map(
        PersonnelFileSettlement settlement,
        IReadOnlyCollection<SettlementWarningResponse>? warnings = null) =>
        new(
            settlement.PublicId,
            settlement.Kind,
            settlement.StatusCode,
            settlement.RetirementRequestPublicId,
            settlement.AssignedPositionPublicId,
            settlement.PositionNameSnapshot,
            settlement.PlazaStartDate,
            settlement.CostCenterPublicId,
            settlement.CostCenterNameSnapshot,
            settlement.RetirementDate,
            settlement.RetirementCategoryCode,
            settlement.RetirementCategoryNameSnapshot,
            settlement.RetirementReasonCode,
            settlement.RetirementReasonNameSnapshot,
            settlement.RequesterFilePublicId,
            settlement.RequesterNameSnapshot,
            settlement.RequestDate,
            settlement.Notes,
            settlement.MinimumMonthlyWage,
            settlement.IndemnityCapMultiplier,
            settlement.ResignationCapMultiplier,
            settlement.VacationDays,
            settlement.VacationPremiumPercent,
            settlement.AguinaldoDays,
            settlement.ResignationBenefitDays,
            settlement.ResignationMinimumServiceYears,
            settlement.AguinaldoExemptionMultiplier,
            settlement.MonthDivisorDays,
            settlement.YearDivisorDays,
            settlement.MonthlyBaseSalary,
            settlement.SeniorityYears,
            settlement.SeniorityDays,
            settlement.CappedMonthlySalaryIndemnity,
            settlement.CappedMonthlySalaryResignation,
            settlement.TotalIncomes,
            settlement.TotalDeductions,
            settlement.NetPay,
            settlement.TotalEmployerCharges,
            settlement.ProvisionTotal,
            settlement.CurrencyCode,
            settlement.IssuedByUserId,
            settlement.IssuedAtUtc,
            settlement.AnnulledByUserId,
            settlement.AnnulledAtUtc,
            settlement.AnnulmentReason,
            settlement.Lines
                .OrderBy(line => line.SortOrder)
                .ThenBy(line => line.ConceptCode, StringComparer.Ordinal)
                .Select(Map)
                .ToArray(),
            warnings ?? SettlementWarningSupport.FromEntity(settlement),
            settlement.ConcurrencyToken,
            settlement.CreatedUtc,
            settlement.ModifiedUtc);

    private static PersonnelFileSettlementLineResponse Map(PersonnelFileSettlementLine line) =>
        new(
            line.PublicId,
            line.ConceptClass,
            line.ConceptCode,
            line.ConceptNameSnapshot,
            line.Description,
            line.IsSystemCalculated,
            line.CalculationBase,
            line.UnitsOrDays,
            line.UnitsOverridden,
            line.CalculatedAmount,
            line.ExemptAmount,
            line.TaxableExcessAmount,
            line.OverrideAmount,
            line.OverrideReason,
            line.FinalAmount,
            line.IsIncluded,
            line.IsZeroByLaw,
            line.ZeroReasonCode,
            line.CalculationDetail,
            line.CounterpartyName,
            line.SortOrder);
}

/// <summary>
/// Warnings derivable from the persisted state (GET path). The engine adds the calculation-time ones
/// (e.g. missing Renta brackets) on the write paths.
/// </summary>
internal static class SettlementWarningSupport
{
    public static IReadOnlyCollection<SettlementWarningResponse> FromEntity(PersonnelFileSettlement settlement)
    {
        var warnings = new List<SettlementWarningResponse>();
        foreach (var line in settlement.Lines.Where(line => line is { IsZeroByLaw: true, IsIncluded: true }))
        {
            warnings.Add(new SettlementWarningResponse(SettlementCalculationRules.WarningZeroByLaw, line.ConceptCode));
        }

        if (settlement.NetPay < 0)
        {
            warnings.Add(new SettlementWarningResponse(SettlementCalculationRules.WarningNetNegative, null));
        }

        var included = settlement.Lines
            .Where(line => line.IsIncluded)
            .Select(line => line.ConceptCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (included.Contains(SettlementConceptCodes.Indemnizacion) && included.Contains(SettlementConceptCodes.RenunciaVoluntaria))
        {
            warnings.Add(new SettlementWarningResponse(SettlementCalculationRules.WarningBothCompensations, null));
        }

        if (settlement.CostCenterPublicId is null)
        {
            warnings.Add(new SettlementWarningResponse("SETTLEMENT_WARNING_NO_COST_CENTER", null));
        }

        return warnings;
    }
}
