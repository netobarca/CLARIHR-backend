using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobProfileFunction : TenantEntity
{
    private JobProfileFunction()
    {
    }

    private JobProfileFunction(
        JobFunctionType functionType,
        long? frequencyCatalogItemId,
        string description,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        FunctionType = functionType;
        FrequencyCatalogItemId = frequencyCatalogItemId;
        Description = JobProfileNormalization.Clean(description, nameof(description));
        SortOrder = sortOrder;
    }

    public long JobProfileId { get; private set; }

    public JobProfile JobProfile { get; private set; } = null!;

    public JobFunctionType FunctionType { get; private set; }

    public long? FrequencyCatalogItemId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public static JobProfileFunction Create(
        JobFunctionType functionType,
        long? frequencyCatalogItemId,
        string description,
        int sortOrder) =>
        new(functionType, frequencyCatalogItemId, description, sortOrder);
}
