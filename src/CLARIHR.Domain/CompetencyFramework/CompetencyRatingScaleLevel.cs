using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CompetencyFramework;

/// <summary>
/// One ordered level of a <see cref="CompetencyRatingScale"/> of type <see cref="CompetencyRatingScaleType.Discrete"/>
/// (e.g. "A" → 5, "B" → 4 …). <c>Value</c> is the ordinal used to compute the competency gap; <c>Label</c> is
/// what the UI shows. Mirrors the child-collection pattern of <see cref="CompetencyConductBehavior"/>.
/// </summary>
public sealed class CompetencyRatingScaleLevel : TenantEntity
{
    public const int MaxCodeLength = 20;
    public const int MaxLabelLength = 120;

    private CompetencyRatingScaleLevel()
    {
    }

    private CompetencyRatingScaleLevel(Guid publicId, string code, string label, decimal value, int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        PublicId = publicId;
        SetCode(code);
        SetLabel(label);
        Value = value;
        SortOrder = sortOrder;
    }

    public long CompetencyRatingScaleId { get; private set; }

    public CompetencyRatingScale CompetencyRatingScale { get; private set; } = null!;

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public decimal Value { get; private set; }

    public int SortOrder { get; private set; }

    public static CompetencyRatingScaleLevel Create(string code, string label, decimal value, int sortOrder) =>
        new(Guid.NewGuid(), code, label, value, sortOrder);

    public void BindToScale(long scaleId) => CompetencyRatingScaleId = scaleId;

    private void SetCode(string code)
    {
        Code = CompetencyFrameworkNormalization.NormalizeCode(code);
        if (Code.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Code must be {MaxCodeLength} characters or fewer.", nameof(code));
        }

        NormalizedCode = Code;
    }

    private void SetLabel(string label)
    {
        Label = CompetencyFrameworkNormalization.Clean(label, nameof(label));
        if (Label.Length > MaxLabelLength)
        {
            throw new ArgumentException($"Label must be {MaxLabelLength} characters or fewer.", nameof(label));
        }
    }
}
