using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CommercialAddon : AuditableEntity
{
    private CommercialAddon()
    {
    }

    private CommercialAddon(
        Guid publicId,
        string code,
        string name,
        string? description,
        CommercialAddonType type,
        decimal pricePerActiveEmployee,
        decimal? minimumMonthlyFee,
        CommercialAddonPeriodicity periodicity,
        CommercialAddonStatus status)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Commercial addon type is invalid.");
        }

        if (!Enum.IsDefined(periodicity))
        {
            throw new ArgumentOutOfRangeException(nameof(periodicity), "Commercial addon periodicity is invalid.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Commercial addon status is invalid.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        Description = CompanyNormalization.CleanOptional(description);
        Type = type;
        PricePerActiveEmployee = NormalizeAmount(pricePerActiveEmployee, nameof(pricePerActiveEmployee));
        MinimumMonthlyFee = NormalizeOptionalAmount(minimumMonthlyFee, nameof(minimumMonthlyFee));
        Periodicity = periodicity;
        Status = status;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public CommercialAddonType Type { get; private set; }

    public decimal PricePerActiveEmployee { get; private set; }

    public decimal? MinimumMonthlyFee { get; private set; }

    public CommercialAddonPeriodicity Periodicity { get; private set; }

    public CommercialAddonStatus Status { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static CommercialAddon Create(
        string code,
        string name,
        string? description,
        CommercialAddonType type,
        decimal pricePerActiveEmployee,
        decimal? minimumMonthlyFee,
        CommercialAddonPeriodicity periodicity,
        CommercialAddonStatus status) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            description,
            type,
            pricePerActiveEmployee,
            minimumMonthlyFee,
            periodicity,
            status);

    public void Update(
        string code,
        string name,
        string? description,
        CommercialAddonType type,
        decimal pricePerActiveEmployee,
        decimal? minimumMonthlyFee,
        CommercialAddonPeriodicity periodicity)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Commercial addon type is invalid.");
        }

        if (!Enum.IsDefined(periodicity))
        {
            throw new ArgumentOutOfRangeException(nameof(periodicity), "Commercial addon periodicity is invalid.");
        }

        SetCode(code);
        SetName(name);
        Description = CompanyNormalization.CleanOptional(description);
        Type = type;
        PricePerActiveEmployee = NormalizeAmount(pricePerActiveEmployee, nameof(pricePerActiveEmployee));
        MinimumMonthlyFee = NormalizeOptionalAmount(minimumMonthlyFee, nameof(minimumMonthlyFee));
        Periodicity = periodicity;
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        if (Status == CommercialAddonStatus.Active)
        {
            throw new InvalidOperationException("Commercial addon is already active.");
        }

        Status = CommercialAddonStatus.Active;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        if (Status == CommercialAddonStatus.Inactive)
        {
            throw new InvalidOperationException("Commercial addon is already inactive.");
        }

        Status = CommercialAddonStatus.Inactive;
        RefreshConcurrencyToken();
    }

    private void SetCode(string code)
    {
        Code = CompanyNormalization.NormalizePlanCode(code);
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = CompanyNormalization.Clean(name, nameof(name));
        NormalizedName = CompanyNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();

    private static decimal NormalizeAmount(decimal value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Amount must be greater than or equal to zero.");
        }

        return value;
    }

    private static decimal? NormalizeOptionalAmount(decimal? value, string parameterName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return NormalizeAmount(value.Value, parameterName);
    }
}
