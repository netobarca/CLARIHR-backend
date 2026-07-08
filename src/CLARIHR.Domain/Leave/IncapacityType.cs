using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Leave;

/// <summary>
/// Company-managed master of incapacity types ("tipos de incapacidad"). The optional
/// deduction/income texts are informative labels for the payroll mapping, and
/// <see cref="AppliesToWorkAccident"/> flags the types tied to a work accident.
/// </summary>
public sealed class IncapacityType : TenantEntity
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 200;
    public const int MaxDeductionTypeTextLength = 150;
    public const int MaxIncomeTypeTextLength = 150;

    private IncapacityType()
    {
    }

    private IncapacityType(
        Guid publicId,
        string code,
        string name,
        string? deductionTypeText,
        string? incomeTypeText,
        bool appliesToWorkAccident)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        SetDeductionTypeText(deductionTypeText);
        SetIncomeTypeText(incomeTypeText);
        AppliesToWorkAccident = appliesToWorkAccident;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? DeductionTypeText { get; private set; }

    public string? IncomeTypeText { get; private set; }

    public bool AppliesToWorkAccident { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static IncapacityType Create(
        string code,
        string name,
        string? deductionTypeText,
        string? incomeTypeText,
        bool appliesToWorkAccident) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            deductionTypeText,
            incomeTypeText,
            appliesToWorkAccident);

    public void Update(
        string code,
        string name,
        string? deductionTypeText,
        string? incomeTypeText,
        bool appliesToWorkAccident)
    {
        SetCode(code);
        SetName(name);
        SetDeductionTypeText(deductionTypeText);
        SetIncomeTypeText(incomeTypeText);
        AppliesToWorkAccident = appliesToWorkAccident;
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
        Code = LeaveNormalization.NormalizeCode(code);
        if (Code.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Code must be {MaxCodeLength} characters or fewer.", nameof(code));
        }

        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = LeaveNormalization.Clean(name, nameof(name));
        if (Name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));
        }

        NormalizedName = LeaveNormalization.NormalizeName(Name);
    }

    private void SetDeductionTypeText(string? deductionTypeText)
    {
        var cleaned = LeaveNormalization.CleanOptional(deductionTypeText);
        if (cleaned is { Length: > MaxDeductionTypeTextLength })
        {
            throw new ArgumentException(
                $"Deduction type text must be {MaxDeductionTypeTextLength} characters or fewer.",
                nameof(deductionTypeText));
        }

        DeductionTypeText = cleaned;
    }

    private void SetIncomeTypeText(string? incomeTypeText)
    {
        var cleaned = LeaveNormalization.CleanOptional(incomeTypeText);
        if (cleaned is { Length: > MaxIncomeTypeTextLength })
        {
            throw new ArgumentException(
                $"Income type text must be {MaxIncomeTypeTextLength} characters or fewer.",
                nameof(incomeTypeText));
        }

        IncomeTypeText = cleaned;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
