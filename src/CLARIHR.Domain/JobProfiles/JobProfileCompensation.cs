using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobProfileCompensation : TenantEntity
{
    private JobProfileCompensation()
    {
    }

    private JobProfileCompensation(
        long? salaryClassCatalogItemId,
        JobCatalogItem? salaryClassCatalogItem,
        string? salaryClassName,
        decimal? minSalary,
        decimal? maxSalary,
        string? currencyCode,
        string? workSchedule,
        bool isPrimary)
    {
        if (minSalary.HasValue && minSalary.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minSalary), "Min salary must be greater than or equal to zero.");
        }

        if (maxSalary.HasValue && maxSalary.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSalary), "Max salary must be greater than or equal to zero.");
        }

        if (minSalary.HasValue && maxSalary.HasValue && minSalary.Value > maxSalary.Value)
        {
            throw new InvalidOperationException("Min salary cannot be greater than max salary.");
        }

        SalaryClassCatalogItem = salaryClassCatalogItem;
        SalaryClassCatalogItemId = salaryClassCatalogItem?.Id ?? salaryClassCatalogItemId;
        SalaryClassName = JobProfileNormalization.CleanOptional(salaryClassName);
        MinSalary = minSalary;
        MaxSalary = maxSalary;
        CurrencyCode = JobProfileNormalization.CleanOptional(currencyCode);
        WorkSchedule = JobProfileNormalization.CleanOptional(workSchedule);
        IsPrimary = isPrimary;
    }

    public long JobProfileId { get; private set; }

    public JobProfile JobProfile { get; private set; } = null!;

    public long? SalaryClassCatalogItemId { get; private set; }

    public JobCatalogItem? SalaryClassCatalogItem { get; private set; }

    public string? SalaryClassName { get; private set; }

    public decimal? MinSalary { get; private set; }

    public decimal? MaxSalary { get; private set; }

    public string? CurrencyCode { get; private set; }

    public string? WorkSchedule { get; private set; }

    public bool IsPrimary { get; private set; }

    public static JobProfileCompensation Create(
        long? salaryClassCatalogItemId,
        JobCatalogItem? salaryClassCatalogItem,
        string? salaryClassName,
        decimal? minSalary,
        decimal? maxSalary,
        string? currencyCode,
        string? workSchedule,
        bool isPrimary) =>
        new(salaryClassCatalogItemId, salaryClassCatalogItem, salaryClassName, minSalary, maxSalary, currencyCode, workSchedule, isPrimary);
}
