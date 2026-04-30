namespace CLARIHR.Domain.Common;

/// <summary>
/// Base class for system-wide catalog items that are not scoped to any specific country.
/// These catalogs are managed globally by platform operators via Backoffice.
/// </summary>
public abstract class SystemScopedCatalogItem : AuditableEntity
{
    protected SystemScopedCatalogItem()
    {
    }

    protected SystemScopedCatalogItem(
        Guid publicId,
        string code,
        string name,
        bool isActive,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        IsActive = isActive;
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void Update(string code, string name, int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        SetCode(code);
        SetName(name);
        SortOrder = sortOrder;
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RefreshConcurrencyToken();
    }

    protected void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();

    private void SetCode(string code)
    {
        Code = Clean(code, nameof(code), 80).ToUpperInvariant();
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = Clean(name, nameof(name), 200);
        NormalizedName = Name.ToUpperInvariant();
    }

    protected static string Clean(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
        }

        return cleaned;
    }
}
