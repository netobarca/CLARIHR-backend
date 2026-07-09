using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Overtime;

/// <summary>
/// Company-managed master of overtime justification types ("tipos de justificación de hora extra",
/// REQ-007 RF-003): why the overtime was worked (picos de producción, cierre contable, emergencia, etc.).
/// Unlike <see cref="OvertimeType"/> it carries no factor and no payroll effect — only an optional
/// <see cref="Description"/>. The master ships with a seeded template via <c>OvertimeTemplateSeeder</c>,
/// idempotent by normalized code. Mirrors <see cref="OvertimeType"/> / <c>RecognitionType</c>: filtered
/// unique <c>(tenant, normalized_code) WHERE is_active</c>, logical activate/inactivate.
/// </summary>
public sealed class OvertimeJustificationType : TenantEntity
{
    public const int MaxCodeLength = 40;
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 500;

    private OvertimeJustificationType()
    {
    }

    private OvertimeJustificationType(
        Guid publicId,
        string code,
        string name,
        string? description,
        int sortOrder)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        Description = CleanDescription(description);
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Optional free-text description of the justification.</summary>
    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static OvertimeJustificationType Create(
        string code,
        string name,
        string? description,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, description, sortOrder);

    public void Update(
        string code,
        string name,
        string? description,
        int sortOrder)
    {
        SetCode(code);
        SetName(name);
        Description = CleanDescription(description);
        SortOrder = sortOrder;
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

    private void SetCode(string code)
    {
        Code = OvertimeNormalization.NormalizeCode(code);
        if (Code.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Code must be {MaxCodeLength} characters or fewer.", nameof(code));
        }

        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = OvertimeNormalization.Clean(name, nameof(name));
        if (Name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));
        }

        NormalizedName = OvertimeNormalization.NormalizeName(Name);
    }

    private static string? CleanDescription(string? value)
    {
        var cleaned = OvertimeNormalization.CleanOptional(value);
        if (cleaned is not null && cleaned.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                $"Description must be {MaxDescriptionLength} characters or fewer.",
                nameof(value));
        }

        return cleaned;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
