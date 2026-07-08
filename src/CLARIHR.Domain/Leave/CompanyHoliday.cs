using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Leave;

/// <summary>
/// Canonical scope codes for a <see cref="CompanyHoliday"/>.
/// </summary>
public static class CompanyHolidayScopes
{
    public const string Nacional = "NACIONAL";
    public const string Local = "LOCAL";
    public const string Institucional = "INSTITUCIONAL";

    public static readonly IReadOnlyCollection<string> All = [Nacional, Local, Institucional];
}

/// <summary>
/// Company-managed master of holidays ("días de asueto") consumed by the incapacity/vacation
/// day-counting rules. One row per tenant and date; the scope distinguishes national, local and
/// institutional holidays.
/// </summary>
public sealed class CompanyHoliday : TenantEntity
{
    public const int MaxDescriptionLength = 200;

    private CompanyHoliday()
    {
    }

    private CompanyHoliday(
        Guid publicId,
        DateOnly date,
        string description,
        string scopeCode)
    {
        PublicId = publicId;
        SetDate(date);
        SetDescription(description);
        SetScopeCode(scopeCode);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public DateOnly Date { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string ScopeCode { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static CompanyHoliday Create(
        DateOnly date,
        string description,
        string scopeCode) =>
        new(
            Guid.NewGuid(),
            date,
            description,
            scopeCode);

    public void Update(
        DateOnly date,
        string description,
        string scopeCode)
    {
        SetDate(date);
        SetDescription(description);
        SetScopeCode(scopeCode);
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

    private void SetDate(DateOnly date)
    {
        if (date == default)
        {
            throw new ArgumentException("Date is required.", nameof(date));
        }

        Date = date;
    }

    private void SetDescription(string description)
    {
        Description = LeaveNormalization.Clean(description, nameof(description));
        if (Description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Description must be {MaxDescriptionLength} characters or fewer.", nameof(description));
        }
    }

    private void SetScopeCode(string scopeCode)
    {
        var normalized = LeaveNormalization.NormalizeCode(scopeCode);
        if (!CompanyHolidayScopes.All.Contains(normalized))
        {
            throw new ArgumentException(
                $"Scope code '{scopeCode}' is not supported. Allowed codes: {string.Join(", ", CompanyHolidayScopes.All)}.",
                nameof(scopeCode));
        }

        ScopeCode = normalized;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
