using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.Features.JobProfiles;

internal static class JobProfileSubResourceMappers
{
    public static JobProfileRequirementResponse ToResponse(
        this JobProfileRequirement requirement,
        Guid? requirementTypeCatalogItemId,
        Guid? catalogItemId = null) =>
        new(
            requirement.PublicId,
            requirement.CatalogItem?.PublicId ?? catalogItemId,
            requirementTypeCatalogItemId,
            requirement.RequirementType,
            requirement.Description,
            requirement.SortOrder,
            requirement.ConcurrencyToken);

    public static JobProfileFunctionResponse ToResponse(this JobProfileFunction function, Guid? frequencyCatalogItemId) =>
        new(
            function.PublicId,
            function.FunctionType,
            frequencyCatalogItemId,
            function.Description,
            function.SortOrder);

    public static JobProfileRelationResponse ToResponse(this JobProfileRelation relation, Guid? catalogItemId = null) =>
        new(
            relation.PublicId,
            relation.CatalogItem?.PublicId ?? catalogItemId,
            relation.RelationType,
            relation.Counterpart,
            relation.Notes,
            relation.SortOrder);

    public static JobProfileLegacyCompetencyResponse ToLegacyResponse(this JobProfileCompetency competency, Guid? catalogItemId = null) =>
        new(
            competency.PublicId,
            competency.CatalogItem?.PublicId ?? catalogItemId,
            competency.Name,
            competency.ExpectedLevel,
            competency.Notes,
            competency.SortOrder);

    public static JobProfileTrainingResponse ToResponse(this JobProfileTraining training, Guid? catalogItemId = null) =>
        new(
            training.PublicId,
            training.CatalogItem?.PublicId ?? catalogItemId,
            training.Name,
            training.Notes,
            training.SortOrder);

    public static JobProfileBenefitResponse ToResponse(this JobProfileBenefit benefit, Guid? catalogItemId = null) =>
        new(
            benefit.PublicId,
            benefit.CatalogItem?.PublicId ?? catalogItemId,
            benefit.Name,
            benefit.Notes,
            benefit.SortOrder);

    public static JobProfileWorkingConditionResponse ToResponse(
        this JobProfileWorkingCondition workingCondition,
        Guid? workConditionTypeCatalogItemId,
        Guid? catalogItemId = null) =>
        new(
            workingCondition.PublicId,
            workingCondition.CatalogItem?.PublicId ?? catalogItemId,
            workConditionTypeCatalogItemId,
            workingCondition.Name,
            workingCondition.Notes,
            workingCondition.SortOrder,
            workingCondition.ConcurrencyToken);

    public static JobProfileDependentPositionResponse ToResponse(
        this JobProfileDependentPosition dependentPosition,
        JobProfileReferenceResponse dependentProfile) =>
        new(
            dependentPosition.PublicId,
            dependentProfile.Id,
            dependentProfile.Code,
            dependentProfile.Title,
            dependentPosition.Quantity,
            dependentPosition.Notes);
}
