using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.JobProfiles;

public sealed class JobProfileCompensation : TenantEntity
{
    private JobProfileCompensation()
    {
    }

    private JobProfileCompensation(
        long jobProfileId,
        long salaryTabulatorLineId,
        string? notes)
    {
        if (jobProfileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(jobProfileId), "Job profile id must be greater than zero.");
        }

        if (salaryTabulatorLineId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(salaryTabulatorLineId), "Salary tabulator line id must be greater than zero.");
        }

        JobProfileId = jobProfileId;
        SalaryTabulatorLineId = salaryTabulatorLineId;
        Notes = JobProfileNormalization.CleanOptional(notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    public long JobProfileId { get; private set; }

    public long SalaryTabulatorLineId { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static JobProfileCompensation Create(
        long jobProfileId,
        long salaryTabulatorLineId,
        string? notes) =>
        new(jobProfileId, salaryTabulatorLineId, notes);

    public void Update(long salaryTabulatorLineId, string? notes)
    {
        if (salaryTabulatorLineId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(salaryTabulatorLineId), "Salary tabulator line id must be greater than zero.");
        }

        SalaryTabulatorLineId = salaryTabulatorLineId;
        Notes = JobProfileNormalization.CleanOptional(notes);
        ConcurrencyToken = Guid.NewGuid();
    }
}
