using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobProfileDependentPosition : TenantEntity
{
    private JobProfileDependentPosition()
    {
    }

    private JobProfileDependentPosition(long dependentJobProfileId, int quantity, string? notes)
    {
        if (dependentJobProfileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dependentJobProfileId), "Dependent profile id must be greater than zero.");
        }

        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than or equal to zero.");
        }

        DependentJobProfileId = dependentJobProfileId;
        Quantity = quantity;
        Notes = JobProfileNormalization.CleanOptional(notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    public long JobProfileId { get; private set; }

    public JobProfile JobProfile { get; private set; } = null!;

    public long DependentJobProfileId { get; private set; }

    public JobProfile DependentJobProfile { get; private set; } = null!;

    public int Quantity { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static JobProfileDependentPosition Create(long dependentJobProfileId, int quantity, string? notes) =>
        new(dependentJobProfileId, quantity, notes);

    public void Update(long dependentJobProfileId, int quantity, string? notes)
    {
        if (dependentJobProfileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dependentJobProfileId), "Dependent profile id must be greater than zero.");
        }

        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than or equal to zero.");
        }

        DependentJobProfileId = dependentJobProfileId;
        Quantity = quantity;
        Notes = JobProfileNormalization.CleanOptional(notes);
        ConcurrencyToken = Guid.NewGuid();
    }
}
