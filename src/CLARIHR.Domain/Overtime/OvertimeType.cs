using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Overtime;

/// <summary>
/// Company-managed master of overtime types ("tipos de hora extra", REQ-007 RF-002/A.2): HED / HEN /
/// HEDF / HENF, etc. Each type carries a <see cref="DefaultFactor"/> (the reference multiplier applied to
/// the base hour — 2.00 / 2.50 / 4.00 / 5.00 in the El Salvador template, editable per company) and an
/// optional <see cref="PayrollEffectDescription"/> ("condiciones/efecto en el pago" of the levantamiento).
/// The master ships with a seeded template via <c>OvertimeTemplateSeeder</c>, idempotent by normalized
/// code (tenant edits are never overwritten). Mirrors the governed masters (<c>CostCenter</c> /
/// <c>RecognitionType</c>): filtered unique <c>(tenant, normalized_code) WHERE is_active</c>, logical
/// activate/inactivate (an inactive type accepts no new records; historical records keep their snapshot).
/// </summary>
public sealed class OvertimeType : TenantEntity
{
    public const int MaxCodeLength = 40;
    public const int MaxNameLength = 120;
    public const int MaxPayrollEffectDescriptionLength = 500;

    private OvertimeType()
    {
    }

    private OvertimeType(
        Guid publicId,
        string code,
        string name,
        decimal defaultFactor,
        string? payrollEffectDescription,
        int sortOrder)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        SetDefaultFactor(defaultFactor);
        PayrollEffectDescription = CleanPayrollEffect(payrollEffectDescription);
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Reference multiplier applied to the base hour (> 0). Editable per company.</summary>
    public decimal DefaultFactor { get; private set; }

    /// <summary>Optional description of the payroll effect / conditions of this overtime type.</summary>
    public string? PayrollEffectDescription { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static OvertimeType Create(
        string code,
        string name,
        decimal defaultFactor,
        string? payrollEffectDescription,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, defaultFactor, payrollEffectDescription, sortOrder);

    public void Update(
        string code,
        string name,
        decimal defaultFactor,
        string? payrollEffectDescription,
        int sortOrder)
    {
        SetCode(code);
        SetName(name);
        SetDefaultFactor(defaultFactor);
        PayrollEffectDescription = CleanPayrollEffect(payrollEffectDescription);
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

    private void SetDefaultFactor(decimal defaultFactor)
    {
        if (defaultFactor <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultFactor), "Default factor must be greater than zero.");
        }

        DefaultFactor = defaultFactor;
    }

    private static string? CleanPayrollEffect(string? value)
    {
        var cleaned = OvertimeNormalization.CleanOptional(value);
        if (cleaned is not null && cleaned.Length > MaxPayrollEffectDescriptionLength)
        {
            throw new ArgumentException(
                $"Payroll effect description must be {MaxPayrollEffectDescriptionLength} characters or fewer.",
                nameof(value));
        }

        return cleaned;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
