using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CostCenters;

public sealed class CostCenter : TenantEntity
{
    private CostCenter()
    {
    }

    private CostCenter(
        Guid publicId,
        string code,
        string name,
        CostCenterType type,
        string? payrollExpenseAccountCode,
        string? employerContributionAccountCode,
        string? provisionAccountCode,
        string? description)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        Type = type;
        PayrollExpenseAccountCode = CostCenterNormalization.CleanOptional(payrollExpenseAccountCode);
        EmployerContributionAccountCode = CostCenterNormalization.CleanOptional(employerContributionAccountCode);
        ProvisionAccountCode = CostCenterNormalization.CleanOptional(provisionAccountCode);
        Description = CostCenterNormalization.CleanOptional(description);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid PublicId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public CostCenterType Type { get; private set; }

    public string? PayrollExpenseAccountCode { get; private set; }

    public string? EmployerContributionAccountCode { get; private set; }

    public string? ProvisionAccountCode { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static CostCenter Create(
        string code,
        string name,
        CostCenterType type,
        string? payrollExpenseAccountCode,
        string? employerContributionAccountCode,
        string? provisionAccountCode,
        string? description) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            type,
            payrollExpenseAccountCode,
            employerContributionAccountCode,
            provisionAccountCode,
            description);

    public void Update(
        string code,
        string name,
        CostCenterType type,
        string? payrollExpenseAccountCode,
        string? employerContributionAccountCode,
        string? provisionAccountCode,
        string? description)
    {
        SetCode(code);
        SetName(name);
        Type = type;
        PayrollExpenseAccountCode = CostCenterNormalization.CleanOptional(payrollExpenseAccountCode);
        EmployerContributionAccountCode = CostCenterNormalization.CleanOptional(employerContributionAccountCode);
        ProvisionAccountCode = CostCenterNormalization.CleanOptional(provisionAccountCode);
        Description = CostCenterNormalization.CleanOptional(description);
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
        Code = CostCenterNormalization.Clean(code, nameof(code));
        NormalizedCode = CostCenterNormalization.NormalizeCode(code);
    }

    private void SetName(string name)
    {
        Name = CostCenterNormalization.Clean(name, nameof(name));
        NormalizedName = CostCenterNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
