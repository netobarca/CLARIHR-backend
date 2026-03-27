using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CommercialPlanLimit : Entity
{
    private CommercialPlanLimit()
    {
    }

    private CommercialPlanLimit(string limitCode, decimal value)
    {
        SetLimitCode(limitCode);
        SetValue(value);
    }

    public long CommercialPlanId { get; private set; }

    public string LimitCode { get; private set; } = string.Empty;

    public string NormalizedLimitCode { get; private set; } = string.Empty;

    public decimal Value { get; private set; }

    public static CommercialPlanLimit Create(string limitCode, decimal value) =>
        new(limitCode, value);

    private void SetLimitCode(string limitCode)
    {
        LimitCode = CompanyNormalization.NormalizeLimitCode(limitCode);
        NormalizedLimitCode = LimitCode;
    }

    private void SetValue(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Limit value must be greater than or equal to zero.");
        }

        Value = value;
    }
}
