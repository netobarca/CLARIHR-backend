using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.Abstractions.JobProfiles;

public interface IJobProfileRepository
{
    void Add(JobProfile profile);

    Task<JobProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid profileId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingProfileId, CancellationToken cancellationToken);

    Task<long?> ResolveOrgUnitIdAsync(Guid tenantId, Guid orgUnitId, CancellationToken cancellationToken);

    Task<bool> OrgUnitExistsOutsideTenantAsync(Guid orgUnitId, CancellationToken cancellationToken);

    Task<long?> ResolveProfileIdAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken);

    Task<bool> ProfileExistsInTenantAsync(Guid tenantId, long profileId, CancellationToken cancellationToken);

    Task<IReadOnlyList<JobProfileDependencyNodeData>> GetDependencyGraphAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<PagedResponse<JobProfileListItemResponse>> SearchAsync(
        Guid tenantId,
        JobProfileStatus? status,
        Guid? orgUnitId,
        Guid? salaryClassId,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<JobProfileResponse?> GetResponseByIdAsync(Guid profileId, CancellationToken cancellationToken);

    Task<JobProfileVacancyTemplateResponse?> GetVacancyTemplateByIdAsync(Guid profileId, CancellationToken cancellationToken);

    Task<JobProfilePrintResponse?> GetPrintByIdAsync(Guid profileId, CancellationToken cancellationToken);
}
