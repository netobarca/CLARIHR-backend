using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Compensation;

/// <summary>
/// One bracket (tramo) of the configurable income-tax (Renta/ISR) withholding table, per pay period
/// (semanal/quincenal/mensual) and tenant. Stores the legally-published table (D-14): lower/upper bound,
/// fixed fee, percentage over the excess, and the excess base. The actual retention is computed by the
/// future payroll module — here we only store the configurable, editable (D-19) table.
/// </summary>
public sealed class IncomeTaxWithholdingBracket : TenantEntity
{
    private IncomeTaxWithholdingBracket()
    {
    }

    private IncomeTaxWithholdingBracket(
        string payPeriodCode,
        int bracketOrder,
        decimal lowerBound,
        decimal? upperBound,
        decimal fixedFee,
        decimal ratePercent,
        decimal excessOver,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        bool isActive)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        PayPeriodCode = NormalizeCode(payPeriodCode);
        BracketOrder = bracketOrder;
        LowerBound = lowerBound;
        UpperBound = upperBound;
        FixedFee = fixedFee;
        RatePercent = ratePercent;
        ExcessOver = excessOver;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        IsActive = isActive;
    }

    public string PayPeriodCode { get; private set; } = string.Empty;

    public int BracketOrder { get; private set; }

    public decimal LowerBound { get; private set; }

    public decimal? UpperBound { get; private set; }

    public decimal FixedFee { get; private set; }

    public decimal RatePercent { get; private set; }

    public decimal ExcessOver { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static IncomeTaxWithholdingBracket Create(
        string payPeriodCode,
        int bracketOrder,
        decimal lowerBound,
        decimal? upperBound,
        decimal fixedFee,
        decimal ratePercent,
        decimal excessOver,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        bool isActive) =>
        new(
            payPeriodCode,
            bracketOrder,
            lowerBound,
            upperBound,
            fixedFee,
            ratePercent,
            excessOver,
            effectiveFromUtc,
            effectiveToUtc,
            isActive);

    private static string NormalizeCode(string code) =>
        string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("PayPeriodCode cannot be empty.", nameof(code))
            : code.Trim().ToUpperInvariant();
}
