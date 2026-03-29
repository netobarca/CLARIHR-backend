using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CommercialPlan : AuditableEntity
{
    private readonly List<CommercialPlanLimit> _limits = [];

    private CommercialPlan()
    {
    }

    private CommercialPlan(
        Guid publicId,
        string code,
        string name,
        string? description,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        CommercialPlanStatus status,
        bool isSystemPlan,
        IEnumerable<(string LimitCode, decimal Value)> limits)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Commercial plan status is invalid.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        Description = CompanyNormalization.CleanOptional(description);
        BaseMonthlyFee = NormalizeAmount(baseMonthlyFee, nameof(baseMonthlyFee));
        PricePerActiveEmployee = NormalizeAmount(pricePerActiveEmployee, nameof(pricePerActiveEmployee));
        Status = status;
        IsSystemPlan = isSystemPlan;
        ReplaceLimitsInternal(limits);
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal BaseMonthlyFee { get; private set; }

    public decimal PricePerActiveEmployee { get; private set; }

    public CommercialPlanStatus Status { get; private set; }

    public bool IsSystemPlan { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<CommercialPlanLimit> Limits => _limits.AsReadOnly();

    public static CommercialPlan Create(
        string code,
        string name,
        string? description,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        CommercialPlanStatus status,
        bool isSystemPlan,
        IEnumerable<(string LimitCode, decimal Value)> limits) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            description,
            baseMonthlyFee,
            pricePerActiveEmployee,
            status,
            isSystemPlan,
            limits);

    public void Update(
        string code,
        string name,
        string? description,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        IEnumerable<(string LimitCode, decimal Value)> limits)
    {
        SetCode(code);
        SetName(name);
        Description = CompanyNormalization.CleanOptional(description);
        BaseMonthlyFee = NormalizeAmount(baseMonthlyFee, nameof(baseMonthlyFee));
        PricePerActiveEmployee = NormalizeAmount(pricePerActiveEmployee, nameof(pricePerActiveEmployee));
        ReplaceLimitsInternal(limits);
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        if (Status == CommercialPlanStatus.Active)
        {
            throw new InvalidOperationException("Commercial plan is already active.");
        }

        Status = CommercialPlanStatus.Active;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        if (Status == CommercialPlanStatus.Inactive)
        {
            throw new InvalidOperationException("Commercial plan is already inactive.");
        }

        Status = CommercialPlanStatus.Inactive;
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

    private void ReplaceLimitsInternal(IEnumerable<(string LimitCode, decimal Value)> limits)
    {
        if (limits is null)
        {
            throw new ArgumentNullException(nameof(limits));
        }

        var nextLimits = new List<CommercialPlanLimit>();
        var normalizedCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (limitCode, value) in limits)
        {
            var limit = CommercialPlanLimit.Create(limitCode, value);
            if (!normalizedCodes.Add(limit.NormalizedLimitCode))
            {
                throw new InvalidOperationException($"Duplicate commercial plan limit code '{limit.NormalizedLimitCode}' is not allowed.");
            }

            nextLimits.Add(limit);
        }

        _limits.Clear();
        foreach (var limit in nextLimits.OrderBy(limit => limit.NormalizedLimitCode, StringComparer.Ordinal))
        {
            _limits.Add(limit);
        }
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
}
