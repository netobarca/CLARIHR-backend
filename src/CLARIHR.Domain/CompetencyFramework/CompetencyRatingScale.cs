using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CompetencyFramework;

/// <summary>
/// A company-configurable competency rating scale (decision D-04). Supports a continuous numeric range
/// (<see cref="CompetencyRatingScaleType.Numeric"/>: 0–100, 1–5, 0–10) or an ordered set of discrete levels
/// (<see cref="CompetencyRatingScaleType.Discrete"/>: A–F, Básico/Intermedio/Avanzado). Both the expected and
/// the achieved competency scores are expressed in this scale, and the gap (expected − achieved) is computed on
/// the numeric/ordinal value so it works for letter scales too. One scale is active per company.
/// </summary>
public sealed class CompetencyRatingScale : TenantEntity
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 120;

    private readonly List<CompetencyRatingScaleLevel> _levels = [];

    private CompetencyRatingScale()
    {
    }

    private CompetencyRatingScale(
        Guid publicId,
        string code,
        string name,
        CompetencyRatingScaleType scaleType,
        decimal? minValue,
        decimal? maxValue,
        int decimals,
        IEnumerable<CompetencyRatingScaleLevel> levels)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        ApplyDefinition(scaleType, minValue, maxValue, decimals, levels);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public CompetencyRatingScaleType ScaleType { get; private set; }

    public decimal? MinValue { get; private set; }

    public decimal? MaxValue { get; private set; }

    public int Decimals { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public IReadOnlyCollection<CompetencyRatingScaleLevel> Levels => _levels;

    public static CompetencyRatingScale CreateNumeric(string code, string name, decimal minValue, decimal maxValue, int decimals) =>
        new(Guid.NewGuid(), code, name, CompetencyRatingScaleType.Numeric, minValue, maxValue, decimals, []);

    public static CompetencyRatingScale CreateDiscrete(string code, string name, IEnumerable<CompetencyRatingScaleLevel> levels) =>
        new(Guid.NewGuid(), code, name, CompetencyRatingScaleType.Discrete, null, null, 0, levels);

    public void Update(
        string code,
        string name,
        CompetencyRatingScaleType scaleType,
        decimal? minValue,
        decimal? maxValue,
        int decimals,
        IEnumerable<CompetencyRatingScaleLevel> levels)
    {
        SetCode(code);
        SetName(name);
        ApplyDefinition(scaleType, minValue, maxValue, decimals, levels);
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        IsActive = false;
        RefreshConcurrencyToken();
    }

    /// <summary>Whether a score is valid in this scale (numeric: within [min,max]; discrete: equals a level value).</summary>
    public bool IsValueAllowed(decimal value) =>
        ScaleType == CompetencyRatingScaleType.Numeric
            ? MinValue is { } min && MaxValue is { } max && value >= min && value <= max
            : _levels.Any(level => level.Value == value);

    private void ApplyDefinition(
        CompetencyRatingScaleType scaleType,
        decimal? minValue,
        decimal? maxValue,
        int decimals,
        IEnumerable<CompetencyRatingScaleLevel> levels)
    {
        var levelList = levels?.ToList() ?? [];
        ScaleType = scaleType;

        if (scaleType == CompetencyRatingScaleType.Numeric)
        {
            if (minValue is null || maxValue is null)
            {
                throw new ArgumentException("Numeric scales require min and max values.", nameof(minValue));
            }

            if (minValue >= maxValue)
            {
                throw new ArgumentException("Numeric scale min value must be less than max value.", nameof(minValue));
            }

            if (decimals < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(decimals), "Decimals must be greater than or equal to zero.");
            }

            if (levelList.Count > 0)
            {
                throw new ArgumentException("Numeric scales must not define discrete levels.", nameof(levels));
            }

            MinValue = minValue;
            MaxValue = maxValue;
            Decimals = decimals;
            _levels.Clear();
        }
        else
        {
            if (levelList.Count < 2)
            {
                throw new ArgumentException("Discrete scales require at least two levels.", nameof(levels));
            }

            if (levelList.Select(level => level.Value).Distinct().Count() != levelList.Count)
            {
                throw new ArgumentException("Discrete scale levels must have distinct values.", nameof(levels));
            }

            MinValue = null;
            MaxValue = null;
            Decimals = 0;
            _levels.Clear();
            _levels.AddRange(levelList);
        }
    }

    private void SetCode(string code)
    {
        Code = CompetencyFrameworkNormalization.NormalizeCode(code);
        if (Code.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Code must be {MaxCodeLength} characters or fewer.", nameof(code));
        }

        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = CompetencyFrameworkNormalization.Clean(name, nameof(name));
        if (Name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));
        }

        NormalizedName = CompetencyFrameworkNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
