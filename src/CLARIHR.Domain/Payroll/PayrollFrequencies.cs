namespace CLARIHR.Domain.Payroll;

/// <summary>
/// Canonical periods-per-year of the fixed payroll frequencies (catalog <c>pay-periods</c>), OWNED by the
/// payroll masters (REQ-012 §0.4): <c>RecurringDeductionFrequencies.PeriodsPerYear</c> belongs to another
/// module and is deliberately NOT imported. Only MENSUAL/QUINCENAL/SEMANAL have a fixed cadence — a payroll
/// whose type is POR_DIA/POR_OBRA has no canonical count, so <c>PayrollDefinition.TotalPeriods</c> stays free
/// with a SOFT coherence reference to these values (deviations like a 13th aguinaldo run are legitimate; the
/// annual calendar generator of PR-2 derives exactly <c>TotalPeriods</c> ranges from this cadence).
/// </summary>
public static class PayrollFrequencies
{
    public const string Mensual = "MENSUAL";
    public const string Quincenal = "QUINCENAL";
    public const string Semanal = "SEMANAL";

    /// <summary>
    /// Frecuencia ANUAL — un solo periodo al año. Nació para la nómina de aguinaldo (requerimiento
    /// 2026-08-12 §5/§6): «es como tener una nómina quincenal pero será una nómina anual en el periodo donde
    /// la empresa haya establecido el pago». No es exclusiva del aguinaldo: es una cadencia más.
    /// </summary>
    public const string Anual = "ANUAL";

    /// <summary>Canonical periods per year for the fixed frequencies (12 / 24 / 52 / 1).</summary>
    public static bool TryGetPeriodsPerYear(string? payPeriodCode, out int periodsPerYear)
    {
        switch (payPeriodCode?.Trim().ToUpperInvariant())
        {
            case Mensual:
                periodsPerYear = 12;
                return true;
            case Quincenal:
                periodsPerYear = 24;
                return true;
            case Semanal:
                periodsPerYear = 52;
                return true;
            case Anual:
                periodsPerYear = 1;
                return true;
            default:
                periodsPerYear = 0;
                return false;
        }
    }
}
