using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobProfileRequirement : TenantEntity
{
    private JobProfileRequirement()
    {
    }

    private JobProfileRequirement(
        JobRequirementType requirementType,
        long? requirementTypeCatalogItemId,
        long? catalogItemId,
        JobCatalogItem? catalogItem,
        string description,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        RequirementType = requirementType;
        RequirementTypeCatalogItemId = requirementTypeCatalogItemId;
        CatalogItem = catalogItem;
        CatalogItemId = catalogItem?.Id ?? catalogItemId;
        Description = JobProfileNormalization.Clean(description, nameof(description));
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long JobProfileId { get; private set; }

    public JobProfile JobProfile { get; private set; } = null!;

    public JobRequirementType RequirementType { get; private set; }

    public long? RequirementTypeCatalogItemId { get; private set; }

    public long? CatalogItemId { get; private set; }

    public JobCatalogItem? CatalogItem { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static JobProfileRequirement Create(
        JobRequirementType requirementType,
        long? requirementTypeCatalogItemId,
        long? catalogItemId,
        JobCatalogItem? catalogItem,
        string description,
        int sortOrder) =>
        new(requirementType, requirementTypeCatalogItemId, catalogItemId, catalogItem, description, sortOrder);

    public void Update(
        JobRequirementType requirementType,
        long? requirementTypeCatalogItemId,
        long? catalogItemId,
        JobCatalogItem? catalogItem,
        string description,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        RequirementType = requirementType;
        RequirementTypeCatalogItemId = requirementTypeCatalogItemId;
        CatalogItem = catalogItem;
        CatalogItemId = catalogItem?.Id ?? catalogItemId;
        Description = JobProfileNormalization.Clean(description, nameof(description));
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }
}
