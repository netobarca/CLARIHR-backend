using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobProfileFunction : TenantEntity
{
    private JobProfileFunction()
    {
    }

    private JobProfileFunction(JobFunctionType functionType, string description, int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        FunctionType = functionType;
        Description = JobProfileNormalization.Clean(description, nameof(description));
        SortOrder = sortOrder;
    }

    public long JobProfileId { get; private set; }

    public JobProfile JobProfile { get; private set; } = null!;

    public JobFunctionType FunctionType { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public static JobProfileFunction Create(JobFunctionType functionType, string description, int sortOrder) =>
        new(functionType, description, sortOrder);
}
