using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.Abstractions.JobProfiles;

public interface IJobProfileRepository
{
    void Add(JobProfile profile);

    Task<JobProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfile?> GetCoreByIdAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfile?> GetWithRequirementsOnlyAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfile?> GetWithFunctionsOnlyAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfile?> GetWithRelationsOnlyAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfile?> GetWithCompetenciesOnlyAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfile?> GetWithWorkingConditionsOnlyAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfile?> GetWithTrainingsOnlyAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfile?> GetWithBenefitsOnlyAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfile?> GetWithDependentPositionsOnlyAsync(Guid profileId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid profileId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingProfileId, CancellationToken cancellationToken);

    Task<long?> ResolveOrgUnitIdAsync(Guid tenantId, Guid orgUnitId, CancellationToken cancellationToken);

    Task<bool> OrgUnitExistsOutsideTenantAsync(Guid orgUnitId, CancellationToken cancellationToken);

    Task<long?> ResolveProfileIdAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken);
    Task<JobProfileReferenceResponse?> GetReferenceByIdAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken);
    Task<JobProfileReferenceResponse?> GetReferenceByInternalIdAsync(Guid tenantId, long profileId, CancellationToken cancellationToken);

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

    Task<JobProfileEntityResponse?> GetEntityResponseByIdAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfileResponse?> GetResponseByIdAsync(Guid profileId, CancellationToken cancellationToken);
    Task<JobProfileCoreResponse?> GetCoreResponseByIdAsync(Guid profileId, CancellationToken cancellationToken);
    Task<PagedResponse<JobProfileRequirementResponse>?> GetRequirementResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<JobProfileRequirementResponse?> GetRequirementResponseAsync(Guid profileId, Guid requirementId, CancellationToken cancellationToken);
    Task<PagedResponse<JobProfileFunctionResponse>?> GetFunctionResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<JobProfileFunctionResponse?> GetFunctionResponseAsync(Guid profileId, Guid functionId, CancellationToken cancellationToken);
    Task<PagedResponse<JobProfileRelationResponse>?> GetRelationResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<JobProfileRelationResponse?> GetRelationResponseAsync(Guid profileId, Guid relationId, CancellationToken cancellationToken);
    Task<PagedResponse<JobProfileLegacyCompetencyResponse>?> GetLegacyCompetencyResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<JobProfileLegacyCompetencyResponse?> GetLegacyCompetencyResponseAsync(Guid profileId, Guid competencyId, CancellationToken cancellationToken);
    Task<PagedResponse<JobProfileTrainingResponse>?> GetTrainingResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<JobProfileTrainingResponse?> GetTrainingResponseAsync(Guid profileId, Guid trainingId, CancellationToken cancellationToken);
    Task<PagedResponse<JobProfileBenefitResponse>?> GetBenefitResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<JobProfileBenefitResponse?> GetBenefitResponseAsync(Guid profileId, Guid benefitId, CancellationToken cancellationToken);
    Task<PagedResponse<JobProfileWorkingConditionResponse>?> GetWorkingConditionResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<JobProfileWorkingConditionResponse?> GetWorkingConditionResponseAsync(Guid profileId, Guid workingConditionId, CancellationToken cancellationToken);
    Task<PagedResponse<JobProfileDependentPositionResponse>?> GetDependentPositionResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<JobProfileDependentPositionResponse?> GetDependentPositionResponseAsync(Guid profileId, Guid dependentPositionId, CancellationToken cancellationToken);

    /// <summary>Internal use only: used by the async PDF export pipeline. Not exposed as an API endpoint.</summary>
    Task<JobProfilePrintResponse?> GetPrintByIdAsync(Guid profileId, CancellationToken cancellationToken);
}
