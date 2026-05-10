using System.Text.Json.Serialization;

namespace CLARIHR.Application.Features.JobProfiles;

public static class JobProfilesMappers
{
    public static JobProfileCompensationInput? MapCompensation(JobProfileCompensationRequest? value) =>
        value is null
            ? null
            : new JobProfileCompensationInput(
                value.SalaryTabulatorLineId,
                value.ResolvedSalaryClassId,
                value.SalaryClassCode,
                value.CurrencyCode,
                value.MinSalary,
                value.MaxSalary);
}

public sealed class JobProfileCompensationRequest
{
    public Guid? SalaryTabulatorLineId { get; set; }
    public Guid? SalaryClassPublicId { get; set; }
    public Guid? SalaryClassId { get; set; }
    public string? SalaryClassCode { get; set; }
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public string? CurrencyCode { get; set; }
    public string? WorkSchedule { get; set; }
    public bool? IsPrimary { get; set; }

    [JsonIgnore]
    public Guid? ResolvedSalaryClassId => SalaryClassPublicId ?? SalaryClassId;
}
