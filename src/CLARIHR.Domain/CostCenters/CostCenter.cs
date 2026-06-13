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
        long costCenterTypeId,
        string? payrollExpenseAccountCode,
        string? employerContributionAccountCode,
        string? provisionAccountCode,
        string? description)
    {
        if (costCenterTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costCenterTypeId), "Cost center type id must be greater than zero.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        CostCenterTypeId = costCenterTypeId;
        PayrollExpenseAccountCode = CostCenterNormalization.CleanOptional(payrollExpenseAccountCode);
        EmployerContributionAccountCode = CostCenterNormalization.CleanOptional(employerContributionAccountCode);
        ProvisionAccountCode = CostCenterNormalization.CleanOptional(provisionAccountCode);
        Description = CostCenterNormalization.CleanOptional(description);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public long CostCenterTypeId { get; private set; }

    public string? PayrollExpenseAccountCode { get; private set; }

    public string? EmployerContributionAccountCode { get; private set; }

    public string? ProvisionAccountCode { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static CostCenter Create(
        string code,
        string name,
        long costCenterTypeId,
        string? payrollExpenseAccountCode,
        string? employerContributionAccountCode,
        string? provisionAccountCode,
        string? description) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            costCenterTypeId,
            payrollExpenseAccountCode,
            employerContributionAccountCode,
            provisionAccountCode,
            description);

    public void Update(
        string code,
        string name,
        long costCenterTypeId,
        string? payrollExpenseAccountCode,
        string? employerContributionAccountCode,
        string? provisionAccountCode,
        string? description)
    {
        if (costCenterTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costCenterTypeId), "Cost center type id must be greater than zero.");
        }

        SetCode(code);
        SetName(name);
        CostCenterTypeId = costCenterTypeId;
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
        Code = CostCenterNormalization.NormalizeCode(code);
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = CostCenterNormalization.Clean(name, nameof(name));
        NormalizedName = CostCenterNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}
