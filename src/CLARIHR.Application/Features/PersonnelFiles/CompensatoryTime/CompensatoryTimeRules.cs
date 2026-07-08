using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;

/// <summary>Whether a statement movement credits hours into the fund or debits hours from it.</summary>
public enum CompensatoryTimeMovementKind
{
    Credit,
    Absence,
}

/// <summary>
/// One raw fund movement fed to <see cref="CompensatoryTimeRules.BuildStatement"/> — a credit or an absence.
/// <paramref name="Hours"/> is always the positive magnitude (credited or debited); the sign and the
/// running balance are derived by the rules. <paramref name="CreatedUtc"/> is the tie-breaker within a date.
/// </summary>
public sealed record CompensatoryTimeMovement(
    Guid PublicId,
    DateOnly Date,
    DateTime CreatedUtc,
    CompensatoryTimeMovementKind Kind,
    decimal Hours,
    string StatusCode);

/// <summary>
/// One resolved statement line: the signed hours (+ credit / − absence), whether it is annulled (excluded
/// from the balance) and the running fund balance AFTER this movement.
/// </summary>
public sealed record CompensatoryTimeStatementLine(
    Guid PublicId,
    DateOnly Date,
    DateTime CreatedUtc,
    CompensatoryTimeMovementKind Kind,
    decimal SignedHours,
    string StatusCode,
    bool IsAnnulled,
    decimal RunningBalance);

/// <summary>The full estado de cuenta: chronological lines with a running balance plus the fund totals.</summary>
public sealed record CompensatoryTimeStatement(
    IReadOnlyList<CompensatoryTimeStatementLine> Lines,
    decimal TotalCredited,
    decimal TotalDebited,
    decimal Balance);

/// <summary>Result of validating a debit against the fund balance (RN-03).</summary>
public sealed record CompensatoryTimeDebitCheck(bool IsAllowed, decimal Shortfall);

/// <summary>
/// The compensatory-time fund arithmetic (REQ-002 §3.5, golden suite A.4) — 100% pure and deterministic:
/// no clock, no database, no side-effects. This is the SINGLE source of truth for the balance and every
/// derivation (running balance, absence-hours suggestion, cap/annulment/debit supports and the settlement
/// valuation), so the estado de cuenta, the profile balance, the write validations and the liquidation
/// line all cuadran by construction.
/// <para>Single rounding rule (D-19, mirrors <c>SettlementCalculationRules.Round2</c>): half-up
/// away-from-zero, 2 decimals — the ONLY rounding point of the module.</para>
/// </summary>
public static class CompensatoryTimeRules
{
    /// <summary>Single rounding rule of the module (D-19): half-up away-from-zero, 2 decimals.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Credited hours = <c>Round2(worked × factor)</c> (RN-02).</summary>
    public static decimal CreditedHours(decimal hoursWorked, decimal factor)
    {
        if (hoursWorked <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursWorked), "Worked hours must be greater than zero.");
        }

        if (factor <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "Factor must be greater than zero.");
        }

        return Round2(hoursWorked * factor);
    }

    /// <summary>Fund balance = Σ credited − Σ debited (both over VIGENTE movements — RN-03).</summary>
    public static decimal Balance(decimal totalCredited, decimal totalDebited) => totalCredited - totalDebited;

    /// <summary>
    /// Builds the chronological estado de cuenta with a running balance. Movements are ordered by
    /// <see cref="CompensatoryTimeMovement.Date"/> then <see cref="CompensatoryTimeMovement.CreatedUtc"/>;
    /// an ANULADA movement is marked and excluded from both the totals and the running balance.
    /// <paramref name="openingBalance"/> carries the accumulated balance before the page cutoff (0 for the
    /// full range — R-T9).
    /// </summary>
    public static CompensatoryTimeStatement BuildStatement(
        IReadOnlyList<CompensatoryTimeMovement> movements,
        decimal openingBalance = 0m)
    {
        ArgumentNullException.ThrowIfNull(movements);

        var ordered = movements
            .OrderBy(movement => movement.Date)
            .ThenBy(movement => movement.CreatedUtc)
            .ToList();

        var lines = new List<CompensatoryTimeStatementLine>(ordered.Count);
        var totalCredited = 0m;
        var totalDebited = 0m;
        var runningBalance = openingBalance;

        foreach (var movement in ordered)
        {
            var isAnnulled = !CompensatoryTimeStatuses.IsVigente(movement.StatusCode);
            var signedHours = movement.Kind == CompensatoryTimeMovementKind.Credit
                ? movement.Hours
                : -movement.Hours;

            if (!isAnnulled)
            {
                if (movement.Kind == CompensatoryTimeMovementKind.Credit)
                {
                    totalCredited += movement.Hours;
                }
                else
                {
                    totalDebited += movement.Hours;
                }

                runningBalance += signedHours;
            }

            lines.Add(new CompensatoryTimeStatementLine(
                movement.PublicId,
                movement.Date,
                movement.CreatedUtc,
                movement.Kind,
                signedHours,
                movement.StatusCode,
                isAnnulled,
                runningBalance));
        }

        return new CompensatoryTimeStatement(
            lines,
            totalCredited,
            totalDebited,
            openingBalance + Balance(totalCredited, totalDebited));
    }

    /// <summary>
    /// Suggests the hours to debit for an absence range = calendar days excluding the rest day (when
    /// provided) and the holidays, times the standard daily hours. With no calendar (rest day null +
    /// empty holidays — degraded mode D-18) this is simply calendar days × standard daily hours.
    /// </summary>
    public static decimal SuggestAbsenceHours(
        DateOnly startDate,
        DateOnly endDate,
        DayOfWeek? restDay,
        IReadOnlySet<DateOnly> holidays,
        decimal standardDailyHours)
    {
        ArgumentNullException.ThrowIfNull(holidays);

        if (endDate < startDate)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(endDate));
        }

        if (standardDailyHours <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(standardDailyHours), "Standard daily hours must be greater than zero.");
        }

        var workedDays = 0;
        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            if (restDay is { } rest && day.DayOfWeek == rest)
            {
                continue;
            }

            if (holidays.Contains(day))
            {
                continue;
            }

            workedDays++;
        }

        return Round2(workedDays * standardDailyHours);
    }

    /// <summary>
    /// Validates a debit against the fund balance (RN-03). When the debit exceeds the balance the check
    /// fails and carries the <c>Round2</c> shortfall (horas faltantes) for the 422 extensions.
    /// </summary>
    public static CompensatoryTimeDebitCheck ValidateDebit(decimal balance, decimal hoursToDebit)
    {
        if (hoursToDebit <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursToDebit), "Hours to debit must be greater than zero.");
        }

        return hoursToDebit <= balance
            ? new CompensatoryTimeDebitCheck(true, 0m)
            : new CompensatoryTimeDebitCheck(false, Round2(hoursToDebit - balance));
    }

    /// <summary>
    /// Maximum hours of a credit that may be annulled without driving the balance negative (RN-06): the
    /// current balance (annulling a credit removes its credited hours).
    /// </summary>
    public static decimal MaxAnnullable(decimal balance) => Math.Max(0m, balance);

    /// <summary>
    /// Maximum hours still creditable under the balance cap (P-10/RN-11): <c>cap − balance</c> (never
    /// negative). A <c>null</c> cap means no limit (returns <c>null</c>).
    /// </summary>
    public static decimal? MaxCreditable(decimal balance, decimal? cap) =>
        cap is null ? null : Round2(Math.Max(0m, cap.Value - balance));

    /// <summary>Hourly rate = <c>Round2(dailySalary / standardDailyHours)</c> (D-19 valuation).</summary>
    public static decimal HourlyRate(decimal dailySalary, decimal standardDailyHours)
    {
        if (standardDailyHours <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(standardDailyHours), "Standard daily hours must be greater than zero.");
        }

        return Round2(dailySalary / standardDailyHours);
    }

    /// <summary>Settlement amount = <c>Round2(hours × hourlyRate × rateFactor)</c> (D-19 valuation).</summary>
    public static decimal SettlementAmount(decimal hours, decimal hourlyRate, decimal rateFactor) =>
        Round2(hours * hourlyRate * rateFactor);
}
