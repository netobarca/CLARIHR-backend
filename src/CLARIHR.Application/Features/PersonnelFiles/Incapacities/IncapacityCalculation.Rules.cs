using System.Globalization;
using System.Text.Json;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Pure input model of the incapacity engine (vacaciones/incapacidades §3.5): EVERYTHING external
// is resolved beforehand by the data provider (no I/O, no clock here). Amount conventions:
// monthly figures, USD, subsidy rates in percent, day counts as whole days.
// ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One subsidy tranche of the risk snapshot the engine segments against — the day range is ABSOLUTE
/// over the extension chain (RN-03). A <c>null</c> <paramref name="DayTo"/> marks the open-ended tranche.
/// </summary>
internal sealed record IncapacityTrancheParameter(
    int DayFrom,
    int? DayTo,
    decimal SubsidyPercent,
    string PayerCode);

/// <summary>
/// Everything the incapacity engine consumes, resolved (pure — the data provider snapshots salary,
/// rest day, holidays, remaining employer cap and chain offset in one trip).
/// </summary>
internal sealed record IncapacityCalculationInput(
    DateOnly StartDate,
    DateOnly? EndDate,
    bool CountsSeventhDay,
    bool CountsSaturday,
    bool CountsHoliday,
    bool HasSubsidy,
    IReadOnlyList<IncapacityTrancheParameter> Tranches,
    IReadOnlySet<DateOnly> Holidays,
    DayOfWeek RestDay,
    int ChainOffsetDays,
    decimal MonthlyBaseSalary,
    decimal EmployerCapRemaining);

// ── Output model ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One resolved segment of the breakdown: absolute chain day range, the EFFECTIVE payer after the
/// employer-cap resolution (D-27) and the segment's money. For <c>ISSS</c>/<c>EMPRESA</c> rows the
/// amount is the subsidy paid (days × daily × percent); for <c>SIN_PAGO</c> rows the amount is the
/// DISCOUNT charged to the employee — the full unpaid day value (days × daily) — and the percent is
/// the effective 0 (see <see cref="IncapacityCalculationRules"/> remarks).
/// </summary>
internal sealed record IncapacityTrancheDetail(
    int DayFromAbsolute,
    int DayToAbsolute,
    decimal SubsidyPercent,
    string PayerCode,
    int Days,
    decimal Amount);

/// <summary>Non-blocking warning surfaced in the response (`warnings[]`) — code + named parameters.</summary>
internal sealed record IncapacityCalculationWarning(
    string Code,
    IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// The engine's full output: day counts, money aggregates (each the exact sum of its per-segment
/// rounded amounts — no global re-rounding), the per-tranche audit trail and the warnings.
/// <see cref="IsDeferred"/> marks an open-ended incapacity whose breakdown is deferred to closure (D-11).
/// </summary>
internal sealed record IncapacityCalculationResult(
    int CalendarDays,
    int ComputableDays,
    int SubsidizedDays,
    int DiscountDays,
    int EmployerDays,
    decimal MonthlyBaseSalary,
    decimal DailySalary,
    decimal SubsidyAmount,
    decimal DiscountAmount,
    decimal EmployerAmount,
    int EmployerCapConsumed,
    IReadOnlyList<IncapacityTrancheDetail> TrancheDetails,
    IReadOnlyList<IncapacityCalculationWarning> Warnings,
    bool IsDeferred);

/// <summary>
/// The incapacity calculation engine (§3.5, golden suite A.4) — 100% pure and deterministic: no clock,
/// no database, no side-effects. Steps: [1] calendar days = end − start + 1 → [2] computable days by a
/// day-by-day scan excluding the rest day (when the risk does not count the séptimo), Saturdays (when
/// not counted) and holidays (when not counted) — an excluded day joins NEITHER the tranche numbering
/// NOR the amounts → [3] chain numbering: computable day k of this record is absolute day
/// ChainOffsetDays + k (RN-03: an extension continues its chain, it never restarts) → [4] segmentation
/// of the absolute range against the [DayFrom, DayTo] tranches; a risk without subsidy sends everything
/// to SIN_PAGO at 0% → [5] payer resolution with the employer cap (D-27): EMPRESA days consume
/// EmployerCapRemaining in absolute-day order and the excess is reclassified to SIN_PAGO with the
/// INCAPACITY_WARNING_CAP_EXHAUSTED warning; ISSS and SIN_PAGO days never consume the cap → [6] money
/// (D-21): daily = round(monthly / 30, 2) and one rounded amount per segment; aggregates are plain sums
/// of the rounded segments → [7] result with counts, amounts, tranche detail and warnings.
/// <para>
/// Ratified decisions this module encodes:
/// <list type="bullet">
/// <item><b>Single rounding rule</b>: <see cref="Round2"/> (half-up away-from-zero, 2 decimals) is the
/// ONLY rounding point — the daily salary is rounded once and each segment amount is rounded once;
/// aggregates never re-round.</item>
/// <item><b>Discount amount</b> (D-21): a SIN_PAGO day means NOBODY subsidizes it, so the payroll
/// discounts the employee the FULL day value — DiscountAmount = round(days × daily, 2), regardless of
/// the percent of the tranche the day originally belonged to before a cap reclassification. The
/// tranche-detail row of a SIN_PAGO segment therefore carries the effective 0% and the discount amount.</item>
/// <item><b>Days beyond the configured tranches</b> (a bounded last tranche shorter than the chain)
/// fall to SIN_PAGO at 0% — the master normally prevents this (contiguous set, open-ended tail), the
/// engine just never drops a computable day.</item>
/// <item><b>Open-ended record</b> (EndDate null, D-11): the engine validates the inputs and returns an
/// empty breakdown flagged <c>IsDeferred</c>; the real breakdown is computed at closure.</item>
/// </list>
/// </para>
/// </summary>
internal static class IncapacityCalculationRules
{
    // Warning codes (bilingual texts in resx; surfaced as `warnings[]`, never ProblemDetails).
    public const string WarningCapExhausted = "INCAPACITY_WARNING_CAP_EXHAUSTED";

    /// <summary>D-21 divisor: the daily salary is the monthly base over 30.</summary>
    private const decimal MonthDivisorDays = 30m;

    private static readonly JsonSerializerOptions TrancheDetailSerializationOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Single rounding rule of the module (D-21): half-up away-from-zero, 2 decimals.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static IncapacityCalculationResult Calculate(IncapacityCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Tranches);
        ArgumentNullException.ThrowIfNull(input.Holidays);

        if (input.ChainOffsetDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Chain offset days cannot be negative.");
        }

        if (input.MonthlyBaseSalary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Monthly base salary cannot be negative.");
        }

        // [6] Daily salary — rounded ONCE here; every segment amount derives from this rounded figure.
        var dailySalary = Round2(input.MonthlyBaseSalary / MonthDivisorDays);

        // Open-ended incapacity (D-11): the breakdown is deferred to closure — empty result, flagged.
        if (input.EndDate is null)
        {
            return new IncapacityCalculationResult(
                CalendarDays: 0,
                ComputableDays: 0,
                SubsidizedDays: 0,
                DiscountDays: 0,
                EmployerDays: 0,
                input.MonthlyBaseSalary,
                dailySalary,
                SubsidyAmount: 0m,
                DiscountAmount: 0m,
                EmployerAmount: 0m,
                EmployerCapConsumed: 0,
                TrancheDetails: [],
                Warnings: [],
                IsDeferred: true);
        }

        var endDate = input.EndDate.Value;
        if (endDate < input.StartDate)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(input));
        }

        // [1] Calendar days.
        var calendarDays = endDate.DayNumber - input.StartDate.DayNumber + 1;

        // [2] Computable days: day-by-day scan; an excluded day joins neither tranches nor amounts.
        var computableDays = 0;
        for (var day = input.StartDate; day <= endDate; day = day.AddDays(1))
        {
            if (!IsExcluded(day, input))
            {
                computableDays++;
            }
        }

        // [3] Chain numbering: this record covers absolute days [offset + 1, offset + computable] (RN-03).
        var firstAbsolute = input.ChainOffsetDays + 1;
        var lastAbsolute = input.ChainOffsetDays + computableDays;

        // [4]-[6] Segmentation + payer resolution with the employer cap + one rounded amount per segment.
        var details = new List<IncapacityTrancheDetail>();
        var warnings = new List<IncapacityCalculationWarning>();
        var subsidizedDays = 0;
        var discountDays = 0;
        var employerDays = 0;
        var capConsumed = 0;
        decimal subsidyAmount = 0m, discountAmount = 0m, employerAmount = 0m;
        var reclassifiedDays = 0;

        // Whole days of employer cap available (decimal.MaxValue convention = no cap).
        var capRemainingDays = input.EmployerCapRemaining >= int.MaxValue
            ? int.MaxValue
            : (int)Math.Floor(Math.Max(0m, input.EmployerCapRemaining));

        // A risk without subsidy sends every computable day to SIN_PAGO at 0% (step 4).
        var tranches = input.HasSubsidy
            ? input.Tranches.OrderBy(tranche => tranche.DayFrom).ToArray()
            : [];

        var cursor = firstAbsolute;
        foreach (var tranche in tranches)
        {
            if (cursor > lastAbsolute)
            {
                break;
            }

            var trancheEnd = tranche.DayTo ?? lastAbsolute;
            if (trancheEnd < cursor)
            {
                continue;
            }

            // Uncovered gap before this tranche (defensive — the master keeps tranches contiguous).
            if (tranche.DayFrom > cursor)
            {
                var gapEnd = Math.Min(tranche.DayFrom - 1, lastAbsolute);
                AddUnpaidSegment(cursor, gapEnd);
                cursor = gapEnd + 1;
                if (cursor > lastAbsolute)
                {
                    break;
                }
            }

            var segmentEnd = Math.Min(trancheEnd, lastAbsolute);
            var segmentDays = segmentEnd - cursor + 1;
            var payerCode = tranche.PayerCode.ToUpperInvariant();

            switch (payerCode)
            {
                case IncapacityPayerCodes.Empresa:
                    // [5] EMPRESA days consume the cap in absolute-day order; the excess is reclassified.
                    var coveredDays = Math.Min(segmentDays, capRemainingDays);
                    if (coveredDays > 0)
                    {
                        var coveredAmount = Round2(coveredDays * dailySalary * tranche.SubsidyPercent / 100m);
                        details.Add(new IncapacityTrancheDetail(
                            cursor, cursor + coveredDays - 1, tranche.SubsidyPercent,
                            IncapacityPayerCodes.Empresa, coveredDays, coveredAmount));
                        employerDays += coveredDays;
                        employerAmount += coveredAmount;
                        capConsumed += coveredDays;
                        capRemainingDays -= coveredDays;
                    }

                    var excessDays = segmentDays - coveredDays;
                    if (excessDays > 0)
                    {
                        AddUnpaidSegment(cursor + coveredDays, segmentEnd);
                        reclassifiedDays += excessDays;
                    }

                    break;

                case IncapacityPayerCodes.Isss:
                    // ISSS pays its subsidy and never consumes the employer cap.
                    var subsidized = Round2(segmentDays * dailySalary * tranche.SubsidyPercent / 100m);
                    details.Add(new IncapacityTrancheDetail(
                        cursor, segmentEnd, tranche.SubsidyPercent,
                        IncapacityPayerCodes.Isss, segmentDays, subsidized));
                    subsidizedDays += segmentDays;
                    subsidyAmount += subsidized;
                    break;

                default:
                    // SIN_PAGO tranche as configured: full-day discount to the employee.
                    AddUnpaidSegment(cursor, segmentEnd);
                    break;
            }

            cursor = segmentEnd + 1;
        }

        // Trailing computable days beyond the configured tranches (or the whole range without subsidy).
        if (cursor <= lastAbsolute)
        {
            AddUnpaidSegment(cursor, lastAbsolute);
        }

        if (reclassifiedDays > 0)
        {
            warnings.Add(new IncapacityCalculationWarning(
                WarningCapExhausted,
                new Dictionary<string, string>
                {
                    ["reclassifiedDays"] = reclassifiedDays.ToString(CultureInfo.InvariantCulture),
                }));
        }

        return new IncapacityCalculationResult(
            calendarDays,
            computableDays,
            subsidizedDays,
            discountDays,
            employerDays,
            input.MonthlyBaseSalary,
            dailySalary,
            subsidyAmount,
            discountAmount,
            employerAmount,
            capConsumed,
            details,
            warnings,
            IsDeferred: false);

        void AddUnpaidSegment(int fromAbsolute, int toAbsolute)
        {
            var days = toAbsolute - fromAbsolute + 1;

            // Ratified discount semantics (D-21): an unpaid day discounts the FULL day value to the
            // employee — round(days × daily, 2) — with the effective 0% subsidy in the audit row.
            var discount = Round2(days * dailySalary);
            details.Add(new IncapacityTrancheDetail(
                fromAbsolute, toAbsolute, SubsidyPercent: 0m, IncapacityPayerCodes.SinPago, days, discount));
            discountDays += days;
            discountAmount += discount;
        }
    }

    /// <summary>
    /// Stable JSON of the per-tranche audit trail for <c>PersonnelFileIncapacity.TrancheDetailJson</c>
    /// (jsonb): camelCase names in record declaration order via the repo's Web serializer defaults.
    /// </summary>
    public static string SerializeTrancheDetail(IncapacityCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result.TrancheDetails, TrancheDetailSerializationOptions);
    }

    /// <summary>Step [2] exclusion predicate: rest day / Saturday / holiday per the risk's counting flags.</summary>
    private static bool IsExcluded(DateOnly day, IncapacityCalculationInput input)
    {
        if (!input.CountsSeventhDay && day.DayOfWeek == input.RestDay)
        {
            return true;
        }

        if (!input.CountsSaturday && day.DayOfWeek == DayOfWeek.Saturday)
        {
            return true;
        }

        return !input.CountsHoliday && input.Holidays.Contains(day);
    }
}
