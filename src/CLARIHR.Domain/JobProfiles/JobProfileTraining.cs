using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobProfileTraining : TenantEntity
{
    private JobProfileTraining()
    {
    }

    private JobProfileTraining(
        long? catalogItemId,
        JobCatalogItem? catalogItem,
        string name,
        string? notes,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        CatalogItem = catalogItem;
        CatalogItemId = catalogItem?.Id ?? catalogItemId;
        Name = JobProfileNormalization.Clean(name, nameof(name));
        Notes = JobProfileNormalization.CleanOptional(notes);
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long JobProfileId { get; private set; }

    public JobProfile JobProfile { get; private set; } = null!;

    public long? CatalogItemId { get; private set; }

    public JobCatalogItem? CatalogItem { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public int SortOrder { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static JobProfileTraining Create(
        long? catalogItemId,
        JobCatalogItem? catalogItem,
        string name,
        string? notes,
        int sortOrder) =>
        new(catalogItemId, catalogItem, name, notes, sortOrder);

    public void Update(
        long? catalogItemId,
        JobCatalogItem? catalogItem,
        string name,
        string? notes,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        CatalogItem = catalogItem;
        CatalogItemId = catalogItem?.Id ?? catalogItemId;
        Name = JobProfileNormalization.Clean(name, nameof(name));
        Notes = JobProfileNormalization.CleanOptional(notes);
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }
}
