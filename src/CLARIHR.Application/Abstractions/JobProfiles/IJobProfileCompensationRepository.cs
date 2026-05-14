using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.SalaryTabulator;

namespace CLARIHR.Application.Abstractions.JobProfiles;

public interface IJobProfileCompensationRepository
{
    void Add(JobProfileCompensation compensation);

    void Remove(JobProfileCompensation compensation);

    Task<JobProfileCompensation?> GetByPublicIdAsync(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
        CancellationToken cancellationToken);

    Task<JobProfileCompensation?> GetByProfileIdAsync(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken);

    Task<JobProfileCompensationItemResponse?> GetResponseAsync(
        Guid jobProfilePublicId,
        Guid compensationPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<JobProfileCompensationItemResponse>?> GetResponsesByProfileIdAsync(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken);

    Task<bool> ProfileHasCompensationAsync(long jobProfileInternalId, CancellationToken cancellationToken);

    Task<long?> ResolveJobProfileInternalIdAsync(Guid tenantId, Guid jobProfilePublicId, CancellationToken cancellationToken);

    Task<SalaryTabulatorLine?> ResolveSalaryTabulatorLineAsync(Guid tenantId, Guid salaryTabulatorLinePublicId, CancellationToken cancellationToken);
}
