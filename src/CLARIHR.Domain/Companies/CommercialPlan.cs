using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CommercialPlan : AuditableEntity
{
    private readonly List<PlanEntitlement> _entitlements = [];
    private readonly List<CommercialPlanLimit> _limits = [];
    private readonly List<CommercialPlanVersion> _versions = [];

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
        IEnumerable<string> moduleKeys,
        IEnumerable<(string LimitCode, decimal Value)> limits,
        DateTime initialVersionEffectiveFromUtc,
        string currencyCode)
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
        ReplaceEntitlementsInternal(moduleKeys);
        ReplaceLimitsInternal(limits);
        AddVersionInternal(
            versionNumber: 1,
            currencyCode,
            BaseMonthlyFee,
            PricePerActiveEmployee,
            initialVersionEffectiveFromUtc);
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

    public IReadOnlyCollection<PlanEntitlement> Entitlements => _entitlements.AsReadOnly();

    public IReadOnlyCollection<CommercialPlanLimit> Limits => _limits.AsReadOnly();

    public IReadOnlyCollection<CommercialPlanVersion> Versions => _versions.AsReadOnly();

    public static CommercialPlan Create(
        string code,
        string name,
        string? description,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        CommercialPlanStatus status,
        bool isSystemPlan,
        IEnumerable<string> moduleKeys,
        IEnumerable<(string LimitCode, decimal Value)> limits,
        DateTime? initialVersionEffectiveFromUtc = null,
        string currencyCode = "USD") =>
        new(
            Guid.NewGuid(),
            code,
            name,
            description,
            baseMonthlyFee,
            pricePerActiveEmployee,
            status,
            isSystemPlan,
            moduleKeys,
            limits,
            initialVersionEffectiveFromUtc ?? DateTime.UtcNow.Date,
            currencyCode);

    public static CommercialPlan Create(
        string code,
        string name,
        string? description,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        CommercialPlanStatus status,
        bool isSystemPlan,
        IEnumerable<(string LimitCode, decimal Value)> limits,
        DateTime? initialVersionEffectiveFromUtc = null,
        string currencyCode = "USD") =>
        Create(
            code,
            name,
            description,
            baseMonthlyFee,
            pricePerActiveEmployee,
            status,
            isSystemPlan,
            Array.Empty<string>(),
            limits,
            initialVersionEffectiveFromUtc,
            currencyCode);

    public void Update(
        string code,
        string name,
        string? description,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        IEnumerable<string> moduleKeys,
        IEnumerable<(string LimitCode, decimal Value)> limits,
        DateTime? priceEffectiveFromUtc = null,
        string currencyCode = "USD")
    {
        SetCode(code);
        SetName(name);
        Description = CompanyNormalization.CleanOptional(description);
        var normalizedBaseMonthlyFee = NormalizeAmount(baseMonthlyFee, nameof(baseMonthlyFee));
        var normalizedPricePerActiveEmployee = NormalizeAmount(pricePerActiveEmployee, nameof(pricePerActiveEmployee));
        ApplyPricingVersionIfNeeded(
            normalizedBaseMonthlyFee,
            normalizedPricePerActiveEmployee,
            priceEffectiveFromUtc ?? DateTime.UtcNow.Date,
            currencyCode);
        BaseMonthlyFee = normalizedBaseMonthlyFee;
        PricePerActiveEmployee = normalizedPricePerActiveEmployee;
        ReplaceEntitlementsInternal(moduleKeys);
        ReplaceLimitsInternal(limits);
        RefreshConcurrencyToken();
    }

    public void Update(
        string code,
        string name,
        string? description,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        IEnumerable<(string LimitCode, decimal Value)> limits,
        DateTime? priceEffectiveFromUtc = null,
        string currencyCode = "USD") =>
        Update(
            code,
            name,
            description,
            baseMonthlyFee,
            pricePerActiveEmployee,
            Array.Empty<string>(),
            limits,
            priceEffectiveFromUtc,
            currencyCode);

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

    private void ReplaceEntitlementsInternal(IEnumerable<string> moduleKeys)
    {
        if (moduleKeys is null)
        {
            throw new ArgumentNullException(nameof(moduleKeys));
        }

        var nextEntitlements = new List<PlanEntitlement>();
        var normalizedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var moduleKey in moduleKeys)
        {
            var normalizedKey = CommercialModuleCatalog.NormalizeKnownKey(moduleKey);
            if (!normalizedKeys.Add(normalizedKey))
            {
                throw new InvalidOperationException($"Duplicate commercial plan module key '{normalizedKey}' is not allowed.");
            }

            nextEntitlements.Add(PlanEntitlement.Create(Code, normalizedKey));
        }

        _entitlements.Clear();
        foreach (var entitlement in nextEntitlements.OrderBy(entitlement => entitlement.ModuleKey, StringComparer.Ordinal))
        {
            _entitlements.Add(entitlement);
        }
    }

    public CommercialPlanVersion GetVersionEffectiveOn(DateTime effectiveAtUtc)
    {
        var version = _versions
            .Where(version => version.IsEffectiveOn(effectiveAtUtc))
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();

        return version ?? throw new InvalidOperationException("Commercial plan does not define an effective version for the requested date.");
    }

    public CommercialPlanVersion GetCurrentVersion() =>
        _versions
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault()
        ?? throw new InvalidOperationException("Commercial plan does not have a current version.");

    private void ApplyPricingVersionIfNeeded(
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        DateTime effectiveFromUtc,
        string currencyCode)
    {
        var normalizedCurrencyCode = CompanyNormalization.NormalizeCurrencyCode(currencyCode);
        var currentVersion = GetCurrentVersion();
        if (currentVersion.BaseMonthlyFee == baseMonthlyFee &&
            currentVersion.PricePerActiveEmployee == pricePerActiveEmployee &&
            string.Equals(currentVersion.CurrencyCode, normalizedCurrencyCode, StringComparison.Ordinal))
        {
            return;
        }

        if (effectiveFromUtc <= currentVersion.EffectiveFromUtc)
        {
            effectiveFromUtc = currentVersion.EffectiveFromUtc.AddTicks(1);
        }

        currentVersion.Close(effectiveFromUtc);
        AddVersionInternal(
            currentVersion.VersionNumber + 1,
            normalizedCurrencyCode,
            baseMonthlyFee,
            pricePerActiveEmployee,
            effectiveFromUtc);
    }

    private void AddVersionInternal(
        int versionNumber,
        string currencyCode,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        DateTime effectiveFromUtc)
    {
        var version = CommercialPlanVersion.Create(
            Id,
            versionNumber,
            currencyCode,
            baseMonthlyFee,
            pricePerActiveEmployee,
            effectiveFromUtc);
        _versions.Add(version);
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
