using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.Compensation;

public static class IndebtednessErrors
{
    /// <summary>
    /// The projected indebtedness exceeds the applicable ceiling (REQ-010 RF-021). This is a RETRYABLE 422: the
    /// levantamiento is literal — <b>warn, never block</b> — so re-sending the same request with
    /// <c>acknowledgeIndebtednessExceeded = true</c> proceeds and stamps the override footprint.
    /// </summary>
    public static readonly Error LimitExceeded = new(
        "INDEBTEDNESS_LIMIT_EXCEEDED",
        "The deduction would push the employee past the applicable indebtedness limit.",
        ErrorType.UnprocessableEntity);
}

/// <summary>Where an override was confirmed. The same credit can exceed the ceiling twice (at registration and
/// again at authorization, with different figures), so the footprint is a row per event, not a flag.</summary>
public static class IndebtednessOverrideStages
{
    public const string Creacion = "CREACION";
    public const string Autorizacion = "AUTORIZACION";
}

/// <summary>Which parameter produced the applicable ceiling.</summary>
public static class IndebtednessLimitSources
{
    public const string PorTipo = "TIPO";
    public const string Global = "GLOBAL";
}

/// <summary>The employee's status against the ceiling.</summary>
public static class IndebtednessStatuses
{
    public const string Dentro = "DENTRO";
    public const string Excedido = "EXCEDIDO";

    /// <summary>No ceiling applies (neither a per-type row nor the company preference). A legitimate state, not
    /// an error: the whole feature is opt-in by configuration.</summary>
    public const string SinControl = "SIN_CONTROL";
}

/// <summary>One income line of the base: a base-salary concept of an ACTIVE plaza, with the pay period that says
/// how to monthly-ize it.</summary>
public sealed record IndebtednessBaseItem(
    Guid AssignedPositionPublicId,
    string ConceptTypeCode,
    decimal Value,
    string PayPeriodCode);

/// <summary>One debt line of the load: a recurring deduction and its installment. Only VIGENTE, non-statutory
/// credits count (P-12); a SUSPENDIDO one travels with <see cref="IsIncludedInLoad"/> false — visible, not counted.</summary>
public sealed record IndebtednessLoadItem(
    Guid RecurringDeductionPublicId,
    string RecurringDeductionTypeCode,
    string? FinancialInstitution,
    string? Reference,
    decimal InstallmentAmount,
    string InstallmentFrequencyCode,
    string StatusCode,
    bool IsIncludedInLoad);

/// <summary>The verdict: what the employee earns, what they already owe, what the new deduction would add, and
/// whether that crosses the applicable ceiling.</summary>
public sealed record IndebtednessAssessment(
    decimal BaseIncome,
    decimal CurrentLoad,
    decimal NewInstallment,
    decimal ProjectedPercent,
    decimal? LimitPercent,
    string? LimitSource,
    bool IsExceeded)
{
    public string Status => LimitPercent is null
        ? IndebtednessStatuses.SinControl
        : IsExceeded ? IndebtednessStatuses.Excedido : IndebtednessStatuses.Dentro;
}

/// <summary>
/// The indebtedness engine (REQ-010 RN-13). Pure: no I/O, no clock. Everything it needs is handed to it.
/// </summary>
public static class IndebtednessRules
{
    /// <summary>The single rounding rule of the module: half-up, away from zero, 2 decimals.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Brings an amount to its MONTHLY equivalent: <c>value × periodsPerYear ÷ 12</c>.
    /// MENSUAL ×1 · QUINCENAL ×2 · SEMANAL ×52/12 (= 4.3333…, the "×4.33" of the analysis, unrounded) · UNICA ×1/12.
    ///
    /// This did NOT exist anywhere in the codebase: <c>SettlementRepository</c>'s <c>MonthlyBaseSalary</c> takes the
    /// concept value RAW and never looks at the pay period. It is deliberately NOT fixed there — that value feeds the
    /// certified settlement engine, and changing its meaning would move finiquito figures.
    /// </summary>
    public static decimal Monthlyize(decimal amount, string? payPeriodCode) =>
        Round2(amount * RecurringDeductionFrequencies.PeriodsPerYear(payPeriodCode) / 12m);

    /// <summary>The company's monthly income base: Σ of the base-salary concepts of the ACTIVE plazas (P-11).</summary>
    public static decimal ComputeBaseIncome(IReadOnlyCollection<IndebtednessBaseItem> baseItems) =>
        Round2(baseItems.Sum(item => Monthlyize(item.Value, item.PayPeriodCode)));

    /// <summary>The monthly debt load: Σ of the monthly-ized installments of the credits that COUNT (P-12).</summary>
    public static decimal ComputeMonthlyLoad(IReadOnlyCollection<IndebtednessLoadItem> loadItems) =>
        Round2(loadItems
            .Where(item => item.IsIncludedInLoad)
            .Sum(item => Monthlyize(item.InstallmentAmount, item.InstallmentFrequencyCode)));

    /// <summary>
    /// The ceiling that applies to a deduction of <paramref name="candidateTypeCode"/>: the per-type row if there is
    /// one, otherwise the company-wide preference, otherwise none.
    ///
    /// The per-type ceiling PREVAILS even when it is MORE PERMISSIVE than the global one (RF-020: "a bank loan
    /// validates against 25%" — not against <c>min(25, 30)</c>). Granting a type its own ceiling is the point.
    /// </summary>
    public static (decimal? Percent, string? Source) ResolveLimit(
        string? candidateTypeCode,
        decimal? globalLimitPercent,
        IReadOnlyDictionary<string, decimal>? limitsByType)
    {
        if (candidateTypeCode is { Length: > 0 } code
            && limitsByType is not null
            && limitsByType.TryGetValue(code.Trim().ToUpperInvariant(), out var typeLimit))
        {
            return (typeLimit, IndebtednessLimitSources.PorTipo);
        }

        return globalLimitPercent is { } global
            ? (global, IndebtednessLimitSources.Global)
            : (null, null);
    }

    /// <summary>
    /// The whole verdict. <paramref name="newInstallment"/> is null for the plain query (nothing is being added);
    /// for the validation and the simulation it is the candidate's installment, monthly-ized here.
    /// </summary>
    public static IndebtednessAssessment Assess(
        decimal baseIncome,
        IReadOnlyCollection<IndebtednessLoadItem> loadItems,
        decimal? newInstallment,
        string? newInstallmentFrequencyCode,
        decimal? globalLimitPercent,
        IReadOnlyDictionary<string, decimal>? limitsByType,
        string? candidateTypeCode)
    {
        var currentLoad = ComputeMonthlyLoad(loadItems);
        var monthlyNew = newInstallment is { } installment
            ? Monthlyize(installment, newInstallmentFrequencyCode)
            : 0m;

        var (limitPercent, limitSource) = ResolveLimit(candidateTypeCode, globalLimitPercent, limitsByType);

        // No income, no percentage. Dividing would give infinity and would BLOCK an employee whose salary simply
        // is not configured yet — the exact opposite of what the business asked for.
        var projectedPercent = baseIncome > 0m
            ? Round2((currentLoad + monthlyNew) / baseIncome * 100m)
            : 0m;

        // Strictly greater: sitting exactly ON the ceiling does not exceed it. And with no ceiling configured
        // nothing is ever exceeded — that is what keeps REQ-008/009 retrocompatible.
        var isExceeded = limitPercent is { } limit && baseIncome > 0m && projectedPercent > limit;

        return new IndebtednessAssessment(
            baseIncome,
            currentLoad,
            monthlyNew,
            projectedPercent,
            limitPercent,
            limitSource,
            isExceeded);
    }

    /// <summary>
    /// The breakdown the client needs to explain the 422 — and to render the confirmation dialog. It travels as
    /// ROOT members of the ProblemDetails (ProblemDetails.Extensions is [JsonExtensionData], so there is no
    /// "extensions" object on the wire), because the localizer REPLACES the <c>detail</c> with the catalogued
    /// message and any figure written there would be lost.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> ToProblemExtensions(IndebtednessAssessment assessment) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["baseIncome"] = assessment.BaseIncome,
            ["currentLoad"] = assessment.CurrentLoad,
            ["newInstallment"] = assessment.NewInstallment,
            ["projectedPercent"] = assessment.ProjectedPercent,
            ["limitPercent"] = assessment.LimitPercent,
            ["limitSource"] = assessment.LimitSource,
        };
}

/// <summary>
/// Everything the engine needs about one employee, fetched in a single round trip: the income base, the debt load,
/// and the company's parameters. Empty + null everywhere = "no parameters configured" ⇒ no validation.
/// </summary>
public sealed record IndebtednessSnapshotData(
    IReadOnlyCollection<IndebtednessBaseItem> BaseItems,
    IReadOnlyCollection<IndebtednessLoadItem> LoadItems,
    decimal? GlobalLimitPercent,
    IReadOnlyDictionary<string, decimal> LimitsByType);
