using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.InternalCatalogs;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.InternalCatalogs;
using CLARIHR.Application.Features.InternalCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.SalaryTabulator.Common;
using CLARIHR.Domain.InternalCatalogs;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using CLARIHR.Domain.SalaryTabulator;
using Microsoft.Extensions.Logging;
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record JobProfileRequirementResponse(
    Guid RequirementPublicId,
    Guid? CatalogItemPublicId,
    Guid? RequirementTypeCatalogItemPublicId,
    JobRequirementType RequirementType,
    string Description,
    int SortOrder,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => RequirementPublicId;

    [JsonIgnore]
    public Guid? CatalogItemId => CatalogItemPublicId;

    [JsonIgnore]
    public Guid? RequirementTypeCatalogItemId => RequirementTypeCatalogItemPublicId;
}

public sealed record JobProfileFunctionResponse(
    Guid FunctionPublicId,
    Guid? FrequencyCatalogItemPublicId,
    JobFunctionType FunctionType,
    string Description,
    int SortOrder,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => FunctionPublicId;

    [JsonIgnore]
    public Guid? FrequencyCatalogItemId => FrequencyCatalogItemPublicId;
}

public sealed record JobProfileRelationResponse(
    Guid RelationPublicId,
    Guid? CatalogItemPublicId,
    JobRelationType RelationType,
    string Counterpart,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => RelationPublicId;

    [JsonIgnore]
    public Guid? CatalogItemId => CatalogItemPublicId;
}

public sealed record JobProfileCompetencyResponse(
    Guid Id,
    Guid OccupationalPyramidLevelId,
    string OccupationalPyramidLevelCode,
    string OccupationalPyramidLevelName,
    int OccupationalPyramidLevelOrder,
    Guid CompetencyId,
    string CompetencyCode,
    string CompetencyName,
    Guid CompetencyTypeId,
    string CompetencyTypeCode,
    string CompetencyTypeName,
    Guid BehaviorLevelId,
    string BehaviorLevelCode,
    string BehaviorLevelName,
    string? ExpectedEvidence,
    int SortOrder,
    IReadOnlyCollection<JobProfileCompetencyConductResponse> Conducts);

public sealed record JobProfileCompetencyConductResponse(
    Guid ConductId,
    string Description,
    int SortOrder);

public sealed record JobProfileTrainingResponse(
    Guid TrainingPublicId,
    Guid? CatalogItemPublicId,
    string Name,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => TrainingPublicId;

    [JsonIgnore]
    public Guid? CatalogItemId => CatalogItemPublicId;
}

public sealed record JobProfileCompensationResponse(
    Guid? SalaryClassId,
    string? SalaryClassName,
    string? SalaryScaleCode,
    Guid? SalaryTabulatorLineId,
    string? CurrencyCode,
    decimal? BaseAmount,
    decimal? MinAmount,
    decimal? MaxAmount,
    DateTime? ResolvedEffectiveFromUtc,
    DateTime? ResolvedEffectiveToUtc);

public sealed record JobProfileBenefitResponse(
    Guid BenefitPublicId,
    Guid? CatalogItemPublicId,
    string Name,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => BenefitPublicId;

    [JsonIgnore]
    public Guid? CatalogItemId => CatalogItemPublicId;
}

public sealed record JobProfileWorkingConditionResponse(
    Guid WorkingConditionPublicId,
    Guid? CatalogItemPublicId,
    Guid? WorkConditionTypeCatalogItemPublicId,
    string Name,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => WorkingConditionPublicId;

    [JsonIgnore]
    public Guid? CatalogItemId => CatalogItemPublicId;

    [JsonIgnore]
    public Guid? WorkConditionTypeCatalogItemId => WorkConditionTypeCatalogItemPublicId;
}

public sealed record JobProfileDependentPositionResponse(
    Guid DependentPositionPublicId,
    Guid DependentJobProfilePublicId,
    string DependentJobProfileCode,
    string DependentJobProfileTitle,
    int Quantity,
    string? Notes,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => DependentPositionPublicId;

    [JsonIgnore]
    public Guid DependentJobProfileId => DependentJobProfilePublicId;
}

public sealed record JobProfileParentConcurrencyResult(Guid ParentConcurrencyToken);

public sealed record JobProfileReferenceResponse(
    Guid Id,
    string Code,
    string Title);

public sealed record JobProfileLegacyCompetencyResponse(
    Guid CompetencyPublicId,
    Guid? CatalogItemPublicId,
    string Name,
    string? ExpectedLevel,
    string? Notes,
    int SortOrder,
    Guid ConcurrencyToken)
{
    [JsonIgnore]
    public Guid Id => CompetencyPublicId;

    [JsonIgnore]
    public Guid? CatalogItemId => CatalogItemPublicId;
}

public sealed record JobProfileListItemResponse(
    Guid Id,
    string Code,
    string Title,
    JobProfileStatus Status,
    int Version,
    Guid? OrgUnitId,
    string? OrgUnitName,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record JobProfileEntityResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Title,
    JobProfileStatus Status,
    int Version,
    string? Objective,
    Guid OrgUnitId,
    Guid? ReportsToJobProfileId,
    Guid? PositionCategoryId,
    Guid? StrategicObjectiveCatalogItemId,
    Guid? AssignedWorkEquipmentCatalogItemId,
    Guid? ResponsibilityCatalogItemId,
    string? DecisionScope,
    string? AssignedResources,
    string? Responsibilities,
    string? BenefitsSummary,
    string? WorkingConditionSummary,
    string? MarketSalaryReference,
    string? ValuationNotes,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record JobProfileResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Title,
    JobProfileStatus Status,
    int Version,
    string? Objective,
    Guid? OrgUnitId,
    string? OrgUnitName,
    Guid? ReportsToJobProfileId,
    string? ReportsToJobProfileCode,
    string? ReportsToJobProfileTitle,
    Guid? PositionCategoryId,
    Guid? StrategicObjectiveCatalogItemId,
    Guid? AssignedWorkEquipmentCatalogItemId,
    Guid? ResponsibilityCatalogItemId,
    string? DecisionScope,
    string? AssignedResources,
    string? Responsibilities,
    string? BenefitsSummary,
    string? WorkingConditionSummary,
    string? MarketSalaryReference,
    string? ValuationNotes,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive,
    IReadOnlyCollection<JobProfileRequirementResponse> Requirements,
    IReadOnlyCollection<JobProfileFunctionResponse> Functions,
    IReadOnlyCollection<JobProfileRelationResponse> Relations,
    IReadOnlyCollection<JobProfileCompetencyResponse> Competencies,
    IReadOnlyCollection<JobProfileTrainingResponse> Trainings,
    JobProfileCompensationResponse? Compensation,
    IReadOnlyCollection<JobProfileBenefitResponse> Benefits,
    IReadOnlyCollection<JobProfileWorkingConditionResponse> WorkingConditions,
    IReadOnlyCollection<JobProfileDependentPositionResponse> DependentPositions,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record JobProfileCoreResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Title,
    JobProfileStatus Status,
    int Version,
    string? Objective,
    Guid? OrgUnitId,
    string? OrgUnitName,
    Guid? ReportsToJobProfileId,
    string? ReportsToJobProfileCode,
    string? ReportsToJobProfileTitle,
    Guid? PositionCategoryId,
    Guid? StrategicObjectiveCatalogItemId,
    Guid? AssignedWorkEquipmentCatalogItemId,
    Guid? ResponsibilityCatalogItemId,
    string? DecisionScope,
    string? AssignedResources,
    string? Responsibilities,
    string? BenefitsSummary,
    string? WorkingConditionSummary,
    string? MarketSalaryReference,
    string? ValuationNotes,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive,
    JobProfileCompensationResponse? Compensation,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

/// <summary>Internal payload used exclusively by the async PDF export pipeline. Not exposed as an API endpoint.</summary>
public sealed record JobProfilePrintResponse(
    JobProfileResponse Profile,
    DateTime GeneratedAtUtc);


public sealed record JobProfileDependencyNodeData(
    long InternalId,
    Guid Id,
    long? ReportsToInternalId,
    IReadOnlyCollection<long> DependentProfileInternalIds);

public sealed record JobProfileRequirementInput(
    JobRequirementType RequirementType,
    Guid? RequirementTypeCatalogItemId,
    Guid? CatalogItemId,
    string? CatalogCode,
    string? CatalogName,
    string Description,
    int SortOrder);

public sealed record JobProfileFunctionInput(
    JobFunctionType FunctionType,
    Guid? FrequencyCatalogItemId,
    string Description,
    int SortOrder);

public sealed record JobProfileRelationInput(
    JobRelationType RelationType,
    Guid? CatalogItemId,
    string? CatalogCode,
    string? CatalogName,
    string Counterpart,
    string? Notes,
    int SortOrder);

public sealed record JobProfileCompetencyInput(
    Guid? CatalogItemId,
    string? CatalogCode,
    string? CatalogName,
    string Name,
    string? ExpectedLevel,
    string? Notes,
    int SortOrder);

public sealed record JobProfileTrainingInput(
    Guid? CatalogItemId,
    string? CatalogCode,
    string? CatalogName,
    string Name,
    string? Notes,
    int SortOrder);

public sealed record JobProfileBenefitInput(
    Guid? CatalogItemId,
    string? CatalogCode,
    string? CatalogName,
    string Name,
    string? Notes,
    int SortOrder);

public sealed record JobProfileWorkingConditionInput(
    Guid? WorkConditionTypeCatalogItemId,
    Guid? CatalogItemId,
    string? CatalogCode,
    string? CatalogName,
    string Name,
    string? Notes,
    int SortOrder);

public sealed record JobProfileDependentPositionInput(
    Guid DependentJobProfileId,
    int Quantity,
    string? Notes);

public sealed record SearchJobProfilesQuery(
    Guid CompanyId,
    JobProfileStatus? Status,
    Guid? OrgUnitId,
    Guid? SalaryClassId,
    string? Search,
    int PageNumber = 1,
    int PageSize = JobProfileValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<JobProfileListItemResponse>>;

public sealed record GetJobProfileByIdQuery(Guid JobProfileId) : IQuery<JobProfileEntityResponse>;

public sealed record GetJobProfileCoreByIdQuery(Guid JobProfileId) : IQuery<JobProfileCoreResponse>;


public sealed record CreateJobProfileCommand(
    Guid CompanyId,
    string Code,
    string Title,
    string? Objective,
    Guid OrgUnitId,
    Guid? ReportsToJobProfileId,
    Guid? PositionCategoryId,
    Guid? StrategicObjectiveCatalogItemId,
    Guid? AssignedWorkEquipmentCatalogItemId,
    Guid? ResponsibilityCatalogItemId,
    string? DecisionScope,
    string? AssignedResources,
    string? Responsibilities,
    string? BenefitsSummary,
    string? WorkingConditionSummary,
    string? MarketSalaryReference,
    string? ValuationNotes,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool AllowInlineCatalogCreate) : ICommand<JobProfileCoreResponse>;

public sealed record UpdateJobProfileCommand(
    Guid JobProfileId,
    string Code,
    string Title,
    string? Objective,
    Guid OrgUnitId,
    Guid? ReportsToJobProfileId,
    Guid? PositionCategoryId,
    Guid? StrategicObjectiveCatalogItemId,
    Guid? AssignedWorkEquipmentCatalogItemId,
    Guid? ResponsibilityCatalogItemId,
    string? DecisionScope,
    string? AssignedResources,
    string? Responsibilities,
    string? BenefitsSummary,
    string? WorkingConditionSummary,
    string? MarketSalaryReference,
    string? ValuationNotes,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool AllowInlineCatalogCreate,
    Guid ConcurrencyToken) : ICommand<JobProfileCoreResponse>;

public sealed record JobProfilePatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileCommand(
    Guid JobProfileId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<JobProfilePatchOperation> Operations) : ICommand<JobProfileCoreResponse>;

internal sealed class SearchJobProfilesQueryValidator : AbstractValidator<SearchJobProfilesQuery>
{
    public SearchJobProfilesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.OrgUnitId).NotEqual(Guid.Empty).When(static query => query.OrgUnitId.HasValue);
        RuleFor(query => query.SalaryClassId).NotEqual(Guid.Empty).When(static query => query.SalaryClassId.HasValue);
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, JobProfileValidationRules.MaxPageSize);
    }
}

internal sealed class GetJobProfileByIdQueryValidator : AbstractValidator<GetJobProfileByIdQuery>
{
    public GetJobProfileByIdQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class GetJobProfileCoreByIdQueryValidator : AbstractValidator<GetJobProfileCoreByIdQuery>
{
    public GetJobProfileCoreByIdQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}


internal sealed class CreateJobProfileCommandValidator : AbstractValidator<CreateJobProfileCommand>
{
    public CreateJobProfileCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Title).NotEmpty().MaximumLength(180);
        RuleFor(command => command.Objective).MaximumLength(4000);
        RuleFor(command => command.DecisionScope).MaximumLength(4000);
        RuleFor(command => command.AssignedResources).MaximumLength(4000);
        RuleFor(command => command.Responsibilities).MaximumLength(4000);
        RuleFor(command => command.BenefitsSummary).MaximumLength(4000);
        RuleFor(command => command.WorkingConditionSummary).MaximumLength(4000);
        RuleFor(command => command.MarketSalaryReference).MaximumLength(4000);
        RuleFor(command => command.ValuationNotes).MaximumLength(4000);
        RuleFor(command => command.OrgUnitId).NotEmpty();
        RuleFor(command => command.ReportsToJobProfileId)
            .NotEqual(Guid.Empty)
            .When(static command => command.ReportsToJobProfileId.HasValue);
        RuleFor(command => command.PositionCategoryId)
            .NotEqual(Guid.Empty)
            .When(static command => command.PositionCategoryId.HasValue);
        RuleFor(command => command.StrategicObjectiveCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.StrategicObjectiveCatalogItemId.HasValue);
        RuleFor(command => command.AssignedWorkEquipmentCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.AssignedWorkEquipmentCatalogItemId.HasValue);
        RuleFor(command => command.ResponsibilityCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.ResponsibilityCatalogItemId.HasValue);
        RuleFor(command => command)
            .Must(static command => !command.EffectiveFromUtc.HasValue ||
                                   !command.EffectiveToUtc.HasValue ||
                                   command.EffectiveFromUtc.Value <= command.EffectiveToUtc.Value)
            .WithMessage("EffectiveFromUtc must be less than or equal to EffectiveToUtc.");

    }
}

internal sealed class UpdateJobProfileCommandValidator : AbstractValidator<UpdateJobProfileCommand>
{
    public UpdateJobProfileCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Title).NotEmpty().MaximumLength(180);
        RuleFor(command => command.Objective).MaximumLength(4000);
        RuleFor(command => command.DecisionScope).MaximumLength(4000);
        RuleFor(command => command.AssignedResources).MaximumLength(4000);
        RuleFor(command => command.Responsibilities).MaximumLength(4000);
        RuleFor(command => command.BenefitsSummary).MaximumLength(4000);
        RuleFor(command => command.WorkingConditionSummary).MaximumLength(4000);
        RuleFor(command => command.MarketSalaryReference).MaximumLength(4000);
        RuleFor(command => command.ValuationNotes).MaximumLength(4000);
        RuleFor(command => command.OrgUnitId).NotEmpty();
        RuleFor(command => command.ReportsToJobProfileId)
            .NotEqual(Guid.Empty)
            .When(static command => command.ReportsToJobProfileId.HasValue);
        RuleFor(command => command.PositionCategoryId)
            .NotEqual(Guid.Empty)
            .When(static command => command.PositionCategoryId.HasValue);
        RuleFor(command => command.StrategicObjectiveCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.StrategicObjectiveCatalogItemId.HasValue);
        RuleFor(command => command.AssignedWorkEquipmentCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.AssignedWorkEquipmentCatalogItemId.HasValue);
        RuleFor(command => command.ResponsibilityCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static command => command.ResponsibilityCatalogItemId.HasValue);
        RuleFor(command => command)
            .Must(static command => !command.EffectiveFromUtc.HasValue ||
                                   !command.EffectiveToUtc.HasValue ||
                                   command.EffectiveFromUtc.Value <= command.EffectiveToUtc.Value)
            .WithMessage("EffectiveFromUtc must be less than or equal to EffectiveToUtc.");
        RuleFor(command => command.ConcurrencyToken).NotEmpty();

    }
}

internal sealed class PatchJobProfileCommandValidator : AbstractValidator<PatchJobProfileCommand>
{
    public PatchJobProfileCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations)
            .ChildRules(operation =>
            {
                operation.RuleFor(item => item.Op).NotEmpty();
                operation.RuleFor(item => item.Path).NotEmpty();
            });
    }
}

internal sealed class JobProfileRequirementInputValidator : AbstractValidator<JobProfileRequirementInput>
{
    public JobProfileRequirementInputValidator()
    {
        RuleFor(input => input.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static input => input.CatalogItemId.HasValue);
        RuleFor(input => input.RequirementTypeCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static input => input.RequirementTypeCatalogItemId.HasValue);
        RuleFor(input => input.CatalogCode)
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .When(static input => !string.IsNullOrWhiteSpace(input.CatalogCode))
            .WithMessage("CatalogCode format is invalid.");
        RuleFor(input => input.CatalogName).MaximumLength(120);
        RuleFor(input => input.Description).NotEmpty().MaximumLength(1000);
        RuleFor(input => input.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class JobProfileFunctionInputValidator : AbstractValidator<JobProfileFunctionInput>
{
    public JobProfileFunctionInputValidator()
    {
        RuleFor(input => input.FrequencyCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static input => input.FrequencyCatalogItemId.HasValue);
        RuleFor(input => input.Description).NotEmpty().MaximumLength(2000);
        RuleFor(input => input.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class JobProfileRelationInputValidator : AbstractValidator<JobProfileRelationInput>
{
    public JobProfileRelationInputValidator()
    {
        RuleFor(input => input.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static input => input.CatalogItemId.HasValue);
        RuleFor(input => input.CatalogCode)
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .When(static input => !string.IsNullOrWhiteSpace(input.CatalogCode))
            .WithMessage("CatalogCode format is invalid.");
        RuleFor(input => input.CatalogName).MaximumLength(120);
        RuleFor(input => input.Counterpart).NotEmpty().MaximumLength(500);
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleFor(input => input.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class JobProfileCompetencyInputValidator : AbstractValidator<JobProfileCompetencyInput>
{
    public JobProfileCompetencyInputValidator()
    {
        RuleFor(input => input.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static input => input.CatalogItemId.HasValue);
        RuleFor(input => input.CatalogCode)
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .When(static input => !string.IsNullOrWhiteSpace(input.CatalogCode))
            .WithMessage("CatalogCode format is invalid.");
        RuleFor(input => input.CatalogName).MaximumLength(120);
        RuleFor(input => input.Name).NotEmpty().MaximumLength(300);
        RuleFor(input => input.ExpectedLevel).MaximumLength(150);
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleFor(input => input.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class JobProfileTrainingInputValidator : AbstractValidator<JobProfileTrainingInput>
{
    public JobProfileTrainingInputValidator()
    {
        RuleFor(input => input.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static input => input.CatalogItemId.HasValue);
        RuleFor(input => input.CatalogCode)
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .When(static input => !string.IsNullOrWhiteSpace(input.CatalogCode))
            .WithMessage("CatalogCode format is invalid.");
        RuleFor(input => input.CatalogName).MaximumLength(120);
        RuleFor(input => input.Name).NotEmpty().MaximumLength(300);
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleFor(input => input.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class JobProfileBenefitInputValidator : AbstractValidator<JobProfileBenefitInput>
{
    public JobProfileBenefitInputValidator()
    {
        RuleFor(input => input.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static input => input.CatalogItemId.HasValue);
        RuleFor(input => input.CatalogCode)
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .When(static input => !string.IsNullOrWhiteSpace(input.CatalogCode))
            .WithMessage("CatalogCode format is invalid.");
        RuleFor(input => input.CatalogName).MaximumLength(120);
        RuleFor(input => input.Name).NotEmpty().MaximumLength(300);
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleFor(input => input.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class JobProfileWorkingConditionInputValidator : AbstractValidator<JobProfileWorkingConditionInput>
{
    public JobProfileWorkingConditionInputValidator()
    {
        RuleFor(input => input.WorkConditionTypeCatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static input => input.WorkConditionTypeCatalogItemId.HasValue);
        RuleFor(input => input.CatalogItemId)
            .NotEqual(Guid.Empty)
            .When(static input => input.CatalogItemId.HasValue);
        RuleFor(input => input.CatalogCode)
            .MaximumLength(50)
            .Must(JobProfileValidationRules.IsValidCode)
            .When(static input => !string.IsNullOrWhiteSpace(input.CatalogCode))
            .WithMessage("CatalogCode format is invalid.");
        RuleFor(input => input.CatalogName).MaximumLength(120);
        RuleFor(input => input.Name).NotEmpty().MaximumLength(300);
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleFor(input => input.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class JobProfileDependentPositionInputValidator : AbstractValidator<JobProfileDependentPositionInput>
{
    public JobProfileDependentPositionInputValidator()
    {
        RuleFor(input => input.DependentJobProfileId).NotEmpty();
        RuleFor(input => input.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(input => input.Notes).MaximumLength(1000);
    }
}

internal sealed class SearchJobProfilesQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchJobProfilesQuery, PagedResponse<JobProfileListItemResponse>>
{
    public async Task<Result<PagedResponse<JobProfileListItemResponse>>> Handle(
        SearchJobProfilesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<JobProfileListItemResponse>>.Failure(authorizationResult.Error);
        }

        var payload = await repository.SearchAsync(
            query.CompanyId,
            query.Status,
            query.OrgUnitId,
            query.SalaryClassId,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<JobProfileListItemResponse>>.Success(payload);
        }

        var canManageProfiles = (await authorizationService.EnsureCanManageProfilesAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = payload.Items
            .Select(item => JobProfilePolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManageProfiles))
            .ToArray();
        payload = payload with { Items = items };

        return Result<PagedResponse<JobProfileListItemResponse>>.Success(payload);
    }
}

internal sealed class GetJobProfileByIdQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetJobProfileByIdQuery, JobProfileEntityResponse>
{
    public async Task<Result<JobProfileEntityResponse>> Handle(GetJobProfileByIdQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileEntityResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileEntityResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetEntityResponseByIdAsync(query.JobProfileId, cancellationToken);
        if (response is not null)
        {
            var canManageProfiles = (await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = JobProfilePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManageProfiles);
            return Result<JobProfileEntityResponse>.Success(response);
        }

        return Result<JobProfileEntityResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : JobProfileErrors.JobProfileNotFound);
    }
}

internal sealed class GetJobProfileCoreByIdQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetJobProfileCoreByIdQuery, JobProfileCoreResponse>
{
    public async Task<Result<JobProfileCoreResponse>> Handle(GetJobProfileCoreByIdQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCoreResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetCoreResponseByIdAsync(query.JobProfileId, cancellationToken);
        if (response is not null)
        {
            var canManageProfiles = (await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = JobProfilePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManageProfiles);
            return Result<JobProfileCoreResponse>.Success(response);
        }

        return Result<JobProfileCoreResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : JobProfileErrors.JobProfileNotFound);
    }
}


internal sealed class CreateJobProfileCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IJobCatalogRepository catalogRepository,
    IInternalCatalogRepository internalCatalogRepository,
    IPositionCatalogLookup positionDescriptionCatalogRepository,
    ISalaryTabulatorRepository salaryTabulatorRepository,
    IAuditService auditService,
    IPlatformAuditService platformAuditService,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    ILogger<CreateJobProfileCommandHandler> logger)
    : ICommandHandler<CreateJobProfileCommand, JobProfileCoreResponse>
{
    public async Task<Result<JobProfileCoreResponse>> Handle(CreateJobProfileCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(command.CompanyId, command.Code.Trim().ToUpperInvariant(), excludingProfileId: null, cancellationToken))
        {
            return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.CodeConflict);
        }

        var orgUnitInternalIdResult = await JobProfileCommandSupport.ResolveOrgUnitInternalIdAsync(
            command.CompanyId,
            command.OrgUnitId,
            authorizationService,
            repository,
            RbacPermissionAction.Create,
            cancellationToken);
        if (orgUnitInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(orgUnitInternalIdResult.Error);
        }

        var reportsToInternalIdResult = await JobProfileCommandSupport.ResolveReportsToInternalIdAsync(
            command.CompanyId,
            command.ReportsToJobProfileId,
            sourceProfilePublicId: null,
            sourceInternalId: null,
            authorizationService,
            repository,
            RbacPermissionAction.Create,
            cancellationToken);
        if (reportsToInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(reportsToInternalIdResult.Error);
        }

        var positionCategoryInternalIdResult = await JobProfileCommandSupport.ResolvePositionCategoryInternalIdAsync(
            command.CompanyId,
            command.PositionCategoryId,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Create,
            cancellationToken);
        if (positionCategoryInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(positionCategoryInternalIdResult.Error);
        }

        var strategicObjectiveInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            command.CompanyId,
            command.StrategicObjectiveCatalogItemId,
            PositionDescriptionCatalogType.StrategicObjective,
            PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Create,
            cancellationToken);
        if (strategicObjectiveInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(strategicObjectiveInternalIdResult.Error);
        }

        var workEquipmentInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            command.CompanyId,
            command.AssignedWorkEquipmentCatalogItemId,
            PositionDescriptionCatalogType.WorkEquipment,
            PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Create,
            cancellationToken);
        if (workEquipmentInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(workEquipmentInternalIdResult.Error);
        }

        var responsibilityInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            command.CompanyId,
            command.ResponsibilityCatalogItemId,
            PositionDescriptionCatalogType.Responsibility,
            PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Create,
            cancellationToken);
        if (responsibilityInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(responsibilityInternalIdResult.Error);
        }

        var inlineDecision = await JobProfileCommandSupport.ResolveInlineCatalogPermissionAsync(
            command.AllowInlineCatalogCreate,
            JobProfileMutationMapper.HasInlineCatalogReferences(command),
            command.CompanyId,
            authorizationService,
            cancellationToken);
        if (inlineDecision.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(inlineDecision.Error);
        }

        var actorResult = await InternalCatalogActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);
        if (actorResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(actorResult.Error);
        }

        if (command.AllowInlineCatalogCreate && inlineDecision.Value)
        {
            logger.LogInformation(
                "Job profile creation requested with AllowInlineCatalogCreate=true. TenantId={TenantId}, ActorId={ActorId}.",
                command.CompanyId,
                actorResult.Value.PublicId);
        }

        var createdCatalogItems = new List<JobCatalogItem>();
        var createdInternalCatalogValues = new List<InternalCatalogValue>();
        var categoryInvalidation = new HashSet<JobCatalogCategory>();

        var mutation = await JobProfileMutationMapper.BuildAsync(
            command.CompanyId,
            command,
            profilePublicId: null,
            profileInternalId: null,
            allowInlineCatalogCreate: inlineDecision.Value,
            authorizationService,
            repository,
            catalogRepository,
            internalCatalogRepository,
            positionDescriptionCatalogRepository,
            salaryTabulatorRepository,
            actorResult.Value.PublicId,
            dateTimeProvider,
            createdCatalogItems,
            createdInternalCatalogValues,
            categoryInvalidation,
            cancellationToken);
        if (mutation.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(mutation.Error);
        }

        if (JobProfileCommandSupport.HasReportsToAlsoAsDependentPosition(
                reportsToInternalIdResult.Value,
                mutation.Value.DependentPositions))
        {
            return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.DependencyCycle);
        }

        var profile = JobProfile.Create(command.Code, command.Title);
        profile.SetTenantId(command.CompanyId);
        profile.UpdateCore(
            command.Code,
            command.Title,
            command.Objective,
            orgUnitInternalIdResult.Value,
            reportsToInternalIdResult.Value,
            positionCategoryInternalIdResult.Value,
            strategicObjectiveInternalIdResult.Value,
            workEquipmentInternalIdResult.Value,
            responsibilityInternalIdResult.Value,
            command.DecisionScope,
            command.AssignedResources,
            command.Responsibilities,
            command.BenefitsSummary,
            command.WorkingConditionSummary,
            command.MarketSalaryReference,
            command.ValuationNotes,
            command.EffectiveFromUtc,
            command.EffectiveToUtc,
            bumpVersion: false);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(profile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await JobProfileCommandSupport.WriteInlineCatalogAuditAsync(
                command.CompanyId,
                createdCatalogItems,
                categoryInvalidation,
                auditService,
                catalogRepository,
                unitOfWork,
                cancellationToken);

            await JobProfileCommandSupport.WriteInternalCatalogAuditAsync(
                createdInternalCatalogValues,
                platformAuditService,
                unitOfWork,
                cancellationToken);

            var response = await repository.GetCoreResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileCreated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Create,
                    $"Created job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCoreResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IJobCatalogRepository catalogRepository,
    IInternalCatalogRepository internalCatalogRepository,
    IPositionCatalogLookup positionDescriptionCatalogRepository,
    ISalaryTabulatorRepository salaryTabulatorRepository,
    IAuditService auditService,
    IPlatformAuditService platformAuditService,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    ILogger<UpdateJobProfileCommandHandler> logger)
    : ICommandHandler<UpdateJobProfileCommand, JobProfileCoreResponse>
{
    public async Task<Result<JobProfileCoreResponse>> Handle(UpdateJobProfileCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCoreResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetCoreByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileCoreResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(profile.TenantId, command.Code.Trim().ToUpperInvariant(), profile.Id, cancellationToken))
        {
            return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.CodeConflict);
        }

        var orgUnitInternalIdResult = await JobProfileCommandSupport.ResolveOrgUnitInternalIdAsync(
            profile.TenantId,
            command.OrgUnitId,
            authorizationService,
            repository,
            RbacPermissionAction.Update,
            cancellationToken);
        if (orgUnitInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(orgUnitInternalIdResult.Error);
        }

        var reportsToInternalIdResult = await JobProfileCommandSupport.ResolveReportsToInternalIdAsync(
            profile.TenantId,
            command.ReportsToJobProfileId,
            sourceProfilePublicId: profile.PublicId,
            sourceInternalId: profile.Id,
            authorizationService,
            repository,
            RbacPermissionAction.Update,
            cancellationToken);
        if (reportsToInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(reportsToInternalIdResult.Error);
        }

        var positionCategoryInternalIdResult = await JobProfileCommandSupport.ResolvePositionCategoryInternalIdAsync(
            profile.TenantId,
            command.PositionCategoryId,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);
        if (positionCategoryInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(positionCategoryInternalIdResult.Error);
        }

        var strategicObjectiveInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            profile.TenantId,
            command.StrategicObjectiveCatalogItemId,
            PositionDescriptionCatalogType.StrategicObjective,
            PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);
        if (strategicObjectiveInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(strategicObjectiveInternalIdResult.Error);
        }

        var workEquipmentInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            profile.TenantId,
            command.AssignedWorkEquipmentCatalogItemId,
            PositionDescriptionCatalogType.WorkEquipment,
            PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);
        if (workEquipmentInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(workEquipmentInternalIdResult.Error);
        }

        var responsibilityInternalIdResult = await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
            profile.TenantId,
            command.ResponsibilityCatalogItemId,
            PositionDescriptionCatalogType.Responsibility,
            PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
            positionDescriptionCatalogRepository,
            RbacPermissionAction.Update,
            cancellationToken);
        if (responsibilityInternalIdResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(responsibilityInternalIdResult.Error);
        }

        var inlineDecision = await JobProfileCommandSupport.ResolveInlineCatalogPermissionAsync(
            command.AllowInlineCatalogCreate,
            JobProfileMutationMapper.HasInlineCatalogReferences(command),
            profile.TenantId,
            authorizationService,
            cancellationToken);
        if (inlineDecision.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(inlineDecision.Error);
        }

        var actorResult = await InternalCatalogActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);
        if (actorResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(actorResult.Error);
        }

        if (command.AllowInlineCatalogCreate && inlineDecision.Value)
        {
            logger.LogInformation(
                "Job profile update requested with AllowInlineCatalogCreate=true. TenantId={TenantId}, ProfileId={ProfileId}, ActorId={ActorId}.",
                profile.TenantId,
                command.JobProfileId,
                actorResult.Value.PublicId);
        }

        var createdCatalogItems = new List<JobCatalogItem>();
        var createdInternalCatalogValues = new List<InternalCatalogValue>();
        var categoryInvalidation = new HashSet<JobCatalogCategory>();

        var mutation = await JobProfileMutationMapper.BuildAsync(
            profile.TenantId,
            command,
            profile.PublicId,
            profile.Id,
            inlineDecision.Value,
            authorizationService,
            repository,
            catalogRepository,
            internalCatalogRepository,
            positionDescriptionCatalogRepository,
            salaryTabulatorRepository,
            actorResult.Value.PublicId,
            dateTimeProvider,
            createdCatalogItems,
            createdInternalCatalogValues,
            categoryInvalidation,
            cancellationToken);
        if (mutation.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(mutation.Error);
        }

        if (JobProfileCommandSupport.HasReportsToAlsoAsDependentPosition(
                reportsToInternalIdResult.Value,
                mutation.Value.DependentPositions))
        {
            return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.DependencyCycle);
        }

        if (profile.Status == JobProfileStatus.Published &&
            !JobProfileCommandSupport.MeetsPublishedMinimumRequirements(
                command.Objective,
                command.Responsibilities,
                mutation.Value.Requirements,
                mutation.Value.Functions))
        {
            return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.PublishRequirementsMissing);
        }

        var before = await repository.GetCoreResponseByIdAsync(profile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                profile.UpdateCore(
                    command.Code,
                    command.Title,
                    command.Objective,
                    orgUnitInternalIdResult.Value,
                    reportsToInternalIdResult.Value,
                    positionCategoryInternalIdResult.Value,
                    strategicObjectiveInternalIdResult.Value,
                    workEquipmentInternalIdResult.Value,
                    responsibilityInternalIdResult.Value,
                    command.DecisionScope,
                    command.AssignedResources,
                    command.Responsibilities,
                    command.BenefitsSummary,
                    command.WorkingConditionSummary,
                    command.MarketSalaryReference,
                    command.ValuationNotes,
                    command.EffectiveFromUtc,
                    command.EffectiveToUtc);
            }
            catch (InvalidOperationException)
            {
                return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.StateConflict);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await JobProfileCommandSupport.WriteInlineCatalogAuditAsync(
                profile.TenantId,
                createdCatalogItems,
                categoryInvalidation,
                auditService,
                catalogRepository,
                unitOfWork,
                cancellationToken);

            await JobProfileCommandSupport.WriteInternalCatalogAuditAsync(
                createdInternalCatalogValues,
                platformAuditService,
                unitOfWork,
                cancellationToken);

            var after = await repository.GetCoreResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileUpdated,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCoreResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobProfileCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IPositionCatalogLookup positionDescriptionCatalogRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchJobProfileCommand, JobProfileCoreResponse>
{
    public async Task<Result<JobProfileCoreResponse>> Handle(PatchJobProfileCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCoreResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCoreResponse>.Failure(authorizationResult.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
            if (profile is null)
            {
                var error = await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound;

                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(error);
            }

            var before = await repository.GetCoreResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved before patch.");

            if (profile.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
            }

            var patchState = JobProfilePatchState.From(profile, before);
            var patchApplication = JobProfilePatchApplier.Apply(command.Operations, patchState);
            if (patchApplication.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(patchApplication.Error);
            }

            var validation = JobProfilePatchApplier.Validate(patchState);
            if (validation.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(validation.Error);
            }

            if (patchState.StatusTouched &&
                patchState.Status == JobProfileStatus.Archived &&
                profile.Status == JobProfileStatus.Archived)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Success(before);
            }

            if (patchState.StatusTouched &&
                patchState.Status == JobProfileStatus.Draft &&
                profile.Status != JobProfileStatus.Draft)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.StateConflict);
            }

            if (await repository.CodeExistsAsync(profile.TenantId, patchState.Code.Trim().ToUpperInvariant(), profile.Id, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.CodeConflict);
            }

            var orgUnitInternalIdResult = patchState.OrgUnitTouched
                ? await JobProfileCommandSupport.ResolveOrgUnitInternalIdAsync(
                    profile.TenantId,
                    patchState.OrgUnitPublicId,
                    authorizationService,
                    repository,
                    RbacPermissionAction.Update,
                    cancellationToken)
                : Result<long>.Success(profile.OrgUnitId);
            if (orgUnitInternalIdResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(orgUnitInternalIdResult.Error);
            }

            var reportsToInternalIdResult = patchState.ReportsToJobProfileTouched
                ? await JobProfileCommandSupport.ResolveReportsToInternalIdAsync(
                    profile.TenantId,
                    patchState.ReportsToJobProfilePublicId,
                    sourceProfilePublicId: profile.PublicId,
                    sourceInternalId: profile.Id,
                    authorizationService,
                    repository,
                    RbacPermissionAction.Update,
                    cancellationToken)
                : Result<long?>.Success(profile.ReportsToJobProfileId);
            if (reportsToInternalIdResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(reportsToInternalIdResult.Error);
            }

            var positionCategoryInternalIdResult = patchState.PositionCategoryTouched
                ? await JobProfileCommandSupport.ResolvePositionCategoryInternalIdAsync(
                    profile.TenantId,
                    patchState.PositionCategoryPublicId,
                    positionDescriptionCatalogRepository,
                    RbacPermissionAction.Update,
                    cancellationToken)
                : Result<long?>.Success(profile.PositionCategoryId);
            if (positionCategoryInternalIdResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(positionCategoryInternalIdResult.Error);
            }

            var strategicObjectiveInternalIdResult = patchState.StrategicObjectiveTouched
                ? await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
                    profile.TenantId,
                    patchState.StrategicObjectiveCatalogItemPublicId,
                    PositionDescriptionCatalogType.StrategicObjective,
                    PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
                    positionDescriptionCatalogRepository,
                    RbacPermissionAction.Update,
                    cancellationToken)
                : Result<long?>.Success(profile.StrategicObjectiveCatalogItemId);
            if (strategicObjectiveInternalIdResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(strategicObjectiveInternalIdResult.Error);
            }

            var workEquipmentInternalIdResult = patchState.AssignedWorkEquipmentTouched
                ? await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
                    profile.TenantId,
                    patchState.AssignedWorkEquipmentCatalogItemPublicId,
                    PositionDescriptionCatalogType.WorkEquipment,
                    PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
                    positionDescriptionCatalogRepository,
                    RbacPermissionAction.Update,
                    cancellationToken)
                : Result<long?>.Success(profile.AssignedWorkEquipmentCatalogItemId);
            if (workEquipmentInternalIdResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(workEquipmentInternalIdResult.Error);
            }

            var responsibilityInternalIdResult = patchState.ResponsibilityTouched
                ? await JobProfileCommandSupport.ResolvePositionDescriptionCatalogItemInternalIdAsync(
                    profile.TenantId,
                    patchState.ResponsibilityCatalogItemPublicId,
                    PositionDescriptionCatalogType.Responsibility,
                    PositionDescriptionCatalogErrors.RelatedCatalogItemNotFound,
                    positionDescriptionCatalogRepository,
                    RbacPermissionAction.Update,
                    cancellationToken)
                : Result<long?>.Success(profile.ResponsibilityCatalogItemId);
            if (responsibilityInternalIdResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(responsibilityInternalIdResult.Error);
            }

            if (JobProfileCommandSupport.HasReportsToAlsoAsDependentPosition(
                    reportsToInternalIdResult.Value,
                    profile.DependentPositions))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.DependencyCycle);
            }

            if (profile.Status == JobProfileStatus.Published &&
                !JobProfileCommandSupport.MeetsPublishedMinimumRequirements(
                    patchState.Objective,
                    patchState.Responsibilities,
                    profile.Requirements,
                    profile.Functions))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.PublishRequirementsMissing);
            }

            try
            {
                profile.UpdateCore(
                    patchState.Code,
                    patchState.Title,
                    patchState.Objective,
                    orgUnitInternalIdResult.Value,
                    reportsToInternalIdResult.Value,
                    positionCategoryInternalIdResult.Value,
                    strategicObjectiveInternalIdResult.Value,
                    workEquipmentInternalIdResult.Value,
                    responsibilityInternalIdResult.Value,
                    patchState.DecisionScope,
                    patchState.AssignedResources,
                    patchState.Responsibilities,
                    patchState.BenefitsSummary,
                    patchState.WorkingConditionSummary,
                    patchState.MarketSalaryReference,
                    patchState.ValuationNotes,
                    patchState.EffectiveFromUtc,
                    patchState.EffectiveToUtc);
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.StateConflict);
            }

            var statusTransition = JobProfileStatusTransition.None;
            if (patchState.StatusTouched && patchState.Status != profile.Status)
            {
                switch (patchState.Status)
                {
                    case JobProfileStatus.Published:
                        try
                        {
                            profile.Publish();
                        }
                        catch (InvalidOperationException)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return Result<JobProfileCoreResponse>.Failure(JobProfileErrors.PublishRequirementsMissing);
                        }
                        statusTransition = JobProfileStatusTransition.Published;
                        break;
                    case JobProfileStatus.Archived:
                        profile.Archive();
                        statusTransition = JobProfileStatusTransition.Archived;
                        break;
                }
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCoreResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved after patch.");

            var (eventType, action, description) = statusTransition switch
            {
                JobProfileStatusTransition.Published => (AuditEventTypes.JobProfilePublished, AuditActions.Update, $"Published job profile {profile.Code}."),
                JobProfileStatusTransition.Archived => (AuditEventTypes.JobProfileArchived, AuditActions.Archive, $"Archived job profile {profile.Code}."),
                _ => (AuditEventTypes.JobProfileUpdated, AuditActions.Update, $"Patched job profile {profile.Code}."),
            };

            await auditService.LogAsync(
                new AuditLogEntry(
                    eventType,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    action,
                    description,
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCoreResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal enum JobProfileStatusTransition
{
    None = 0,
    Published = 1,
    Archived = 2
}

internal sealed class JobProfilePatchState
{
    private JobProfilePatchState(JobProfile profile, JobProfileCoreResponse before)
    {
        Code = profile.Code;
        Title = profile.Title;
        Objective = profile.Objective;
        OrgUnitPublicId = before.OrgUnitId ?? Guid.Empty;
        ReportsToJobProfilePublicId = before.ReportsToJobProfileId;
        PositionCategoryPublicId = before.PositionCategoryId;
        StrategicObjectiveCatalogItemPublicId = before.StrategicObjectiveCatalogItemId;
        AssignedWorkEquipmentCatalogItemPublicId = before.AssignedWorkEquipmentCatalogItemId;
        ResponsibilityCatalogItemPublicId = before.ResponsibilityCatalogItemId;
        DecisionScope = profile.DecisionScope;
        AssignedResources = profile.AssignedResources;
        Responsibilities = profile.Responsibilities;
        BenefitsSummary = profile.BenefitsSummary;
        WorkingConditionSummary = profile.WorkingConditionSummary;
        MarketSalaryReference = profile.MarketSalaryReference;
        ValuationNotes = profile.ValuationNotes;
        EffectiveFromUtc = profile.EffectiveFromUtc;
        EffectiveToUtc = profile.EffectiveToUtc;
        AllowInlineCatalogCreate = false;
        Status = profile.Status;
    }

    public string Code { get; set; }
    public string Title { get; set; }
    public string? Objective { get; set; }
    public Guid OrgUnitPublicId { get; set; }
    public bool OrgUnitTouched { get; set; }
    public Guid? ReportsToJobProfilePublicId { get; set; }
    public bool ReportsToJobProfileTouched { get; set; }
    public Guid? PositionCategoryPublicId { get; set; }
    public bool PositionCategoryTouched { get; set; }
    public Guid? StrategicObjectiveCatalogItemPublicId { get; set; }
    public bool StrategicObjectiveTouched { get; set; }
    public Guid? AssignedWorkEquipmentCatalogItemPublicId { get; set; }
    public bool AssignedWorkEquipmentTouched { get; set; }
    public Guid? ResponsibilityCatalogItemPublicId { get; set; }
    public bool ResponsibilityTouched { get; set; }
    public string? DecisionScope { get; set; }
    public string? AssignedResources { get; set; }
    public string? Responsibilities { get; set; }
    public string? BenefitsSummary { get; set; }
    public string? WorkingConditionSummary { get; set; }
    public string? MarketSalaryReference { get; set; }
    public string? ValuationNotes { get; set; }
    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool EffectiveRangeTouched { get; set; }
    public bool AllowInlineCatalogCreate { get; set; }
    public JobProfileStatus Status { get; set; }
    public bool StatusTouched { get; set; }

    public static JobProfilePatchState From(JobProfile profile, JobProfileCoreResponse before) => new(profile, before);
}

internal static class JobProfilePatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfilePatchOperation> operations, JobProfilePatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!SupportedOperations.Contains(op))
            {
                return ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = ParsePath(operation.Path);
            if (segments.Length == 0)
            {
                return ValidationFailure(operation.Path, "Patch path is required.");
            }

            try
            {
                if (segments.Length != 1)
                {
                    return ValidationFailure(operation.Path, "Only root patch paths are supported.");
                }

                var coreResult = ApplyCoreOperation(op, segments[0], operation.Value, state, operation.Path);
                if (coreResult.IsFailure)
                {
                    return coreResult;
                }
            }
            catch (JobProfilePatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(JobProfilePatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        AddRequired(errors, "code", state.Code);
        AddMaxLength(errors, "code", state.Code, 50);
        if (!string.IsNullOrWhiteSpace(state.Code) && !JobProfileValidationRules.IsValidCode(state.Code))
        {
            errors["code"] = ["Code format is invalid."];
        }

        AddRequired(errors, "title", state.Title);
        AddMaxLength(errors, "title", state.Title, 180);
        AddMaxLength(errors, "objective", state.Objective, 4000);
        AddMaxLength(errors, "decisionScope", state.DecisionScope, 4000);
        AddMaxLength(errors, "assignedResources", state.AssignedResources, 4000);
        AddMaxLength(errors, "responsibilities", state.Responsibilities, 4000);
        AddMaxLength(errors, "benefitsSummary", state.BenefitsSummary, 4000);
        AddMaxLength(errors, "workingConditionSummary", state.WorkingConditionSummary, 4000);
        AddMaxLength(errors, "marketSalaryReference", state.MarketSalaryReference, 4000);
        AddMaxLength(errors, "valuationNotes", state.ValuationNotes, 4000);

        if (state.OrgUnitTouched && state.OrgUnitPublicId == Guid.Empty)
        {
            errors["orgUnitPublicId"] = ["OrgUnitPublicId is required."];
        }

        if (state.EffectiveFromUtc.HasValue &&
            state.EffectiveToUtc.HasValue &&
            state.EffectiveFromUtc.Value > state.EffectiveToUtc.Value)
        {
            errors["effectiveFromUtc"] = ["EffectiveFromUtc must be less than or equal to EffectiveToUtc."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyCoreOperation(
        string op,
        string property,
        JsonElement? value,
        JobProfilePatchState state,
        string path)
    {
        var isRemove = IsRemove(op);
        if (IsSegment(property, "code"))
        {
            state.Code = isRemove ? string.Empty : ReadRequiredString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "title"))
        {
            state.Title = isRemove ? string.Empty : ReadRequiredString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "objective"))
        {
            state.Objective = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsAnySegment(property, "orgUnitPublicId", "orgUnitId"))
        {
            state.OrgUnitPublicId = isRemove ? Guid.Empty : ReadRequiredGuid(value, path);
            state.OrgUnitTouched = true;
            return Result.Success();
        }

        if (IsAnySegment(property, "reportsToJobProfilePublicId", "reportsToJobProfileId"))
        {
            state.ReportsToJobProfilePublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.ReportsToJobProfileTouched = true;
            return Result.Success();
        }

        if (IsAnySegment(property, "positionCategoryPublicId", "positionCategoryId"))
        {
            state.PositionCategoryPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.PositionCategoryTouched = true;
            return Result.Success();
        }

        if (IsAnySegment(property, "strategicObjectiveCatalogItemPublicId", "strategicObjectiveCatalogItemId"))
        {
            state.StrategicObjectiveCatalogItemPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.StrategicObjectiveTouched = true;
            return Result.Success();
        }

        if (IsAnySegment(property, "assignedWorkEquipmentCatalogItemPublicId", "assignedWorkEquipmentCatalogItemId"))
        {
            state.AssignedWorkEquipmentCatalogItemPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.AssignedWorkEquipmentTouched = true;
            return Result.Success();
        }

        if (IsAnySegment(property, "responsibilityCatalogItemPublicId", "responsibilityCatalogItemId"))
        {
            state.ResponsibilityCatalogItemPublicId = isRemove ? null : ReadNullableGuid(value, path);
            state.ResponsibilityTouched = true;
            return Result.Success();
        }

        if (IsSegment(property, "decisionScope"))
        {
            state.DecisionScope = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "assignedResources"))
        {
            state.AssignedResources = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "responsibilities"))
        {
            state.Responsibilities = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "benefitsSummary"))
        {
            state.BenefitsSummary = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "workingConditionSummary"))
        {
            state.WorkingConditionSummary = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "marketSalaryReference"))
        {
            state.MarketSalaryReference = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "valuationNotes"))
        {
            state.ValuationNotes = isRemove ? null : ReadNullableString(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "effectiveFromUtc"))
        {
            state.EffectiveFromUtc = isRemove ? null : ReadNullableDateTime(value, path);
            state.EffectiveRangeTouched = true;
            return Result.Success();
        }

        if (IsSegment(property, "effectiveToUtc"))
        {
            state.EffectiveToUtc = isRemove ? null : ReadNullableDateTime(value, path);
            state.EffectiveRangeTouched = true;
            return Result.Success();
        }

        if (IsSegment(property, "allowInlineCatalogCreate"))
        {
            state.AllowInlineCatalogCreate = !isRemove && ReadBool(value, path);
            return Result.Success();
        }

        if (IsSegment(property, "status"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "Status cannot be removed.");
            }

            state.Status = ReadStatus(value, path);
            state.StatusTouched = true;
            return Result.Success();
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static JobProfileStatus ReadStatus(JsonElement? value, string path)
    {
        var raw = ReadNullableString(value, path);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JobProfilePatchValueException(path, "Status is required.");
        }

        return Enum.TryParse<JobProfileStatus>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(typeof(JobProfileStatus), parsed)
            ? parsed
            : throw new JobProfilePatchValueException(path, $"Status '{raw}' is not a valid value.");
    }

    private static string[] ParsePath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapeJsonPointerSegment)
            .ToArray();

    private static string UnescapeJsonPointerSegment(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

    private static bool IsRemove(string op) => string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

    private static bool IsNull(JsonElement? value) =>
        !value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static bool IsSegment(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsAnySegment(string actual, params string[] expected) =>
        expected.Any(item => IsSegment(actual, item));

    private static string ReadRequiredString(JsonElement? value, string path) =>
        ReadNullableString(value, path) ?? string.Empty;

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new JobProfilePatchValueException(path, "Value must be a string or null.");
    }

    private static Guid ReadRequiredGuid(JsonElement? value, string path) =>
        ReadNullableGuid(value, path) ?? Guid.Empty;

    private static Guid? ReadNullableGuid(JsonElement? value, string path)
    {
        var raw = ReadNullableString(value, path);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return Guid.TryParse(raw, out var parsed)
            ? parsed
            : throw new JobProfilePatchValueException(path, "Value must be a valid UUID.");
    }

    private static DateTime? ReadNullableDateTime(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        if (value!.Value.ValueKind == JsonValueKind.String && value.Value.TryGetDateTime(out var parsed))
        {
            return parsed;
        }

        throw new JobProfilePatchValueException(path, "Value must be a valid date-time string or null.");
    }

    private static decimal? ReadNullableDecimal(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        if (value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDecimal(out var parsed))
        {
            return parsed;
        }

        var raw = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (!string.IsNullOrWhiteSpace(raw) &&
            decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        throw new JobProfilePatchValueException(path, "Value must be a decimal number or null.");
    }

    private static bool ReadBool(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return false;
        }

        return value!.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new JobProfilePatchValueException(path, "Value must be a boolean.")
        };
    }

    private static Guid? ReadOptionalPropertyGuid(JsonElement element, string propertyName, string path) =>
        TryGetPropertyIgnoreCase(element, propertyName, out var property)
            ? ReadNullableGuid(property, $"{path}/{propertyName}")
            : null;

    private static string? ReadOptionalPropertyString(JsonElement element, string propertyName, string path) =>
        TryGetPropertyIgnoreCase(element, propertyName, out var property)
            ? ReadNullableString(property, $"{path}/{propertyName}")
            : null;

    private static decimal? ReadOptionalPropertyDecimal(JsonElement element, string propertyName, string path) =>
        TryGetPropertyIgnoreCase(element, propertyName, out var property)
            ? ReadNullableDecimal(property, $"{path}/{propertyName}")
            : null;

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement property)
    {
        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));

    private static void AddRequired(Dictionary<string, string[]> errors, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[key] = [$"{key} is required."];
        }
    }

    private static void AddMaxLength(Dictionary<string, string[]> errors, string key, string? value, int maxLength)
    {
        if (value is not null && value.Length > maxLength)
        {
            errors[key] = [$"{key} must be {maxLength} characters or fewer."];
        }
    }
}

internal sealed class JobProfilePatchValueException(string path, string message) : Exception(message)
{
    public string Path { get; } = path;
}

internal sealed record JobProfileMutation(
    IReadOnlyCollection<JobProfileRequirement> Requirements,
    IReadOnlyCollection<JobProfileFunction> Functions,
    IReadOnlyCollection<JobProfileRelation> Relations,
    IReadOnlyCollection<JobProfileCompetency> Competencies,
    IReadOnlyCollection<JobProfileTraining> Trainings,
    IReadOnlyCollection<JobProfileBenefit> Benefits,
    IReadOnlyCollection<JobProfileWorkingCondition> WorkingConditions,
    IReadOnlyCollection<JobProfileDependentPosition> DependentPositions);

internal static class JobProfileMutationMapper
{
    public static bool HasInlineCatalogReferences(CreateJobProfileCommand command) => false;

    public static bool HasInlineCatalogReferences(UpdateJobProfileCommand command) => false;

    public static async Task<Result<JobProfileMutation>> BuildAsync(
        Guid tenantId,
        CreateJobProfileCommand command,
        Guid? profilePublicId,
        long? profileInternalId,
        bool allowInlineCatalogCreate,
        IJobProfileAuthorizationService authorizationService,
        IJobProfileRepository profileRepository,
        IJobCatalogRepository catalogRepository,
        IInternalCatalogRepository internalCatalogRepository,
        IPositionCatalogLookup positionDescriptionCatalogRepository,
        ISalaryTabulatorRepository salaryTabulatorRepository,
        Guid actorUserPublicId,
        IDateTimeProvider dateTimeProvider,
        IList<JobCatalogItem> createdCatalogItems,
        IList<InternalCatalogValue> createdInternalCatalogValues,
        ISet<JobCatalogCategory> categoryInvalidation,
        CancellationToken cancellationToken) =>
        await BuildAsync(
            tenantId,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            command.EffectiveFromUtc,
            command.EffectiveToUtc,
            profilePublicId,
            profileInternalId,
            allowInlineCatalogCreate,
            authorizationService,
            profileRepository,
            catalogRepository,
            internalCatalogRepository,
            positionDescriptionCatalogRepository,
            salaryTabulatorRepository,
            actorUserPublicId,
            dateTimeProvider,
            createdCatalogItems,
            createdInternalCatalogValues,
            categoryInvalidation,
            cancellationToken);

    public static async Task<Result<JobProfileMutation>> BuildAsync(
        Guid tenantId,
        UpdateJobProfileCommand command,
        Guid? profilePublicId,
        long? profileInternalId,
        bool allowInlineCatalogCreate,
        IJobProfileAuthorizationService authorizationService,
        IJobProfileRepository profileRepository,
        IJobCatalogRepository catalogRepository,
        IInternalCatalogRepository internalCatalogRepository,
        IPositionCatalogLookup positionDescriptionCatalogRepository,
        ISalaryTabulatorRepository salaryTabulatorRepository,
        Guid actorUserPublicId,
        IDateTimeProvider dateTimeProvider,
        IList<JobCatalogItem> createdCatalogItems,
        IList<InternalCatalogValue> createdInternalCatalogValues,
        ISet<JobCatalogCategory> categoryInvalidation,
        CancellationToken cancellationToken) =>
        await BuildAsync(
            tenantId,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            command.EffectiveFromUtc,
            command.EffectiveToUtc,
            profilePublicId,
            profileInternalId,
            allowInlineCatalogCreate,
            authorizationService,
            profileRepository,
            catalogRepository,
            internalCatalogRepository,
            positionDescriptionCatalogRepository,
            salaryTabulatorRepository,
            actorUserPublicId,
            dateTimeProvider,
            createdCatalogItems,
            createdInternalCatalogValues,
            categoryInvalidation,
            cancellationToken);

    public static async Task<Result<JobProfileMutation>> BuildAsync(
        Guid tenantId,
        IReadOnlyCollection<JobProfileRequirementInput> requirements,
        IReadOnlyCollection<JobProfileFunctionInput> functions,
        IReadOnlyCollection<JobProfileRelationInput> relations,
        IReadOnlyCollection<JobProfileCompetencyInput> competencies,
        IReadOnlyCollection<JobProfileTrainingInput> trainings,
        IReadOnlyCollection<JobProfileBenefitInput> benefits,
        IReadOnlyCollection<JobProfileWorkingConditionInput> workingConditions,
        IReadOnlyCollection<JobProfileDependentPositionInput> dependentPositions,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc,
        Guid? profilePublicId,
        long? profileInternalId,
        bool allowInlineCatalogCreate,
        IJobProfileAuthorizationService authorizationService,
        IJobProfileRepository profileRepository,
        IJobCatalogRepository catalogRepository,
        IInternalCatalogRepository internalCatalogRepository,
        IPositionCatalogLookup positionDescriptionCatalogRepository,
        ISalaryTabulatorRepository salaryTabulatorRepository,
        Guid actorUserPublicId,
        IDateTimeProvider dateTimeProvider,
        IList<JobCatalogItem> createdCatalogItems,
        IList<InternalCatalogValue> createdInternalCatalogValues,
        ISet<JobCatalogCategory> categoryInvalidation,
        CancellationToken cancellationToken)
    {
        var requirementEntities = new List<JobProfileRequirement>();
        foreach (var input in requirements)
        {
            long? requirementTypeCatalogItemId = null;
            if (input.RequirementTypeCatalogItemId.HasValue)
            {
                var requirementTypeLookup = await positionDescriptionCatalogRepository.GetActiveCatalogReferenceAsync(
                    tenantId,
                    PositionDescriptionCatalogType.RequirementType,
                    input.RequirementTypeCatalogItemId.Value,
                    cancellationToken);
                if (requirementTypeLookup is null)
                {
                    return Result<JobProfileMutation>.Failure(PositionDescriptionCatalogErrors.RequirementTypeNotFound);
                }

                requirementTypeCatalogItemId = requirementTypeLookup.InternalId;
            }

            var category = ResolveRequirementCategory(input.RequirementType);
            var catalogResolution = await ResolveCatalogReferenceAsync(
                tenantId,
                category,
                input.CatalogItemId,
                input.CatalogCode,
                input.CatalogName,
                allowInlineCatalogCreate,
                authorizationService,
                catalogRepository,
                createdCatalogItems,
                categoryInvalidation,
                cancellationToken);
            if (catalogResolution.IsFailure)
            {
                return Result<JobProfileMutation>.Failure(catalogResolution.Error);
            }

            var descriptionResult = await ResolveRequirementDescriptionAsync(
                input.RequirementType,
                input.Description,
                actorUserPublicId,
                internalCatalogRepository,
                dateTimeProvider,
                createdInternalCatalogValues,
                cancellationToken);
            if (descriptionResult.IsFailure)
            {
                return Result<JobProfileMutation>.Failure(descriptionResult.Error);
            }

            requirementEntities.Add(JobProfileRequirement.Create(
                input.RequirementType,
                requirementTypeCatalogItemId,
                catalogResolution.Value?.Id,
                catalogResolution.Value,
                descriptionResult.Value,
                input.SortOrder));
        }

        var functionEntities = new List<JobProfileFunction>(functions.Count);
        foreach (var input in functions)
        {
            long? frequencyCatalogItemId = null;
            if (input.FrequencyCatalogItemId.HasValue)
            {
                var frequencyLookup = await positionDescriptionCatalogRepository.GetActiveCatalogReferenceAsync(
                    tenantId,
                    PositionDescriptionCatalogType.Frequency,
                    input.FrequencyCatalogItemId.Value,
                    cancellationToken);
                if (frequencyLookup is null)
                {
                    return Result<JobProfileMutation>.Failure(PositionDescriptionCatalogErrors.FrequencyNotFound);
                }

                frequencyCatalogItemId = frequencyLookup.InternalId;
            }

            functionEntities.Add(JobProfileFunction.Create(
                input.FunctionType,
                frequencyCatalogItemId,
                input.Description,
                input.SortOrder));
        }

        var relationEntities = new List<JobProfileRelation>();
        foreach (var input in relations)
        {
            var catalogResolution = await ResolveCatalogReferenceAsync(
                tenantId,
                JobCatalogCategory.RelationType,
                input.CatalogItemId,
                input.CatalogCode,
                input.CatalogName,
                allowInlineCatalogCreate,
                authorizationService,
                catalogRepository,
                createdCatalogItems,
                categoryInvalidation,
                cancellationToken);
            if (catalogResolution.IsFailure)
            {
                return Result<JobProfileMutation>.Failure(catalogResolution.Error);
            }

            relationEntities.Add(JobProfileRelation.Create(
                input.RelationType,
                catalogResolution.Value?.Id,
                catalogResolution.Value,
                input.Counterpart,
                input.Notes,
                input.SortOrder));
        }

        var competencyEntities = new List<JobProfileCompetency>();
        foreach (var input in competencies)
        {
            var catalogResolution = await ResolveCatalogReferenceAsync(
                tenantId,
                JobCatalogCategory.Competency,
                input.CatalogItemId,
                input.CatalogCode,
                input.CatalogName,
                allowInlineCatalogCreate,
                authorizationService,
                catalogRepository,
                createdCatalogItems,
                categoryInvalidation,
                cancellationToken);
            if (catalogResolution.IsFailure)
            {
                return Result<JobProfileMutation>.Failure(catalogResolution.Error);
            }

            var name = string.IsNullOrWhiteSpace(input.Name)
                ? catalogResolution.Value?.Name ?? string.Empty
                : input.Name;

            competencyEntities.Add(JobProfileCompetency.Create(
                catalogResolution.Value?.Id,
                catalogResolution.Value,
                name,
                input.ExpectedLevel,
                input.Notes,
                input.SortOrder));
        }

        var trainingEntities = new List<JobProfileTraining>();
        foreach (var input in trainings)
        {
            var catalogResolution = await ResolveCatalogReferenceAsync(
                tenantId,
                JobCatalogCategory.Training,
                input.CatalogItemId,
                input.CatalogCode,
                input.CatalogName,
                allowInlineCatalogCreate,
                authorizationService,
                catalogRepository,
                createdCatalogItems,
                categoryInvalidation,
                cancellationToken);
            if (catalogResolution.IsFailure)
            {
                return Result<JobProfileMutation>.Failure(catalogResolution.Error);
            }

            var name = string.IsNullOrWhiteSpace(input.Name)
                ? catalogResolution.Value?.Name ?? string.Empty
                : input.Name;

            trainingEntities.Add(JobProfileTraining.Create(
                catalogResolution.Value?.Id,
                catalogResolution.Value,
                name,
                input.Notes,
                input.SortOrder));
        }

        var benefitEntities = new List<JobProfileBenefit>();
        foreach (var input in benefits)
        {
            var catalogResolution = await ResolveCatalogReferenceAsync(
                tenantId,
                JobCatalogCategory.BenefitType,
                input.CatalogItemId,
                input.CatalogCode,
                input.CatalogName,
                allowInlineCatalogCreate,
                authorizationService,
                catalogRepository,
                createdCatalogItems,
                categoryInvalidation,
                cancellationToken);
            if (catalogResolution.IsFailure)
            {
                return Result<JobProfileMutation>.Failure(catalogResolution.Error);
            }

            var name = string.IsNullOrWhiteSpace(input.Name)
                ? catalogResolution.Value?.Name ?? string.Empty
                : input.Name;

            benefitEntities.Add(JobProfileBenefit.Create(
                catalogResolution.Value?.Id,
                catalogResolution.Value,
                name,
                input.Notes,
                input.SortOrder));
        }

        var workingConditionEntities = new List<JobProfileWorkingCondition>();
        foreach (var input in workingConditions)
        {
            long? workConditionTypeCatalogItemId = null;
            if (input.WorkConditionTypeCatalogItemId.HasValue)
            {
                var workConditionTypeLookup = await positionDescriptionCatalogRepository.GetActiveCatalogReferenceAsync(
                    tenantId,
                    PositionDescriptionCatalogType.WorkConditionType,
                    input.WorkConditionTypeCatalogItemId.Value,
                    cancellationToken);
                if (workConditionTypeLookup is null)
                {
                    return Result<JobProfileMutation>.Failure(PositionDescriptionCatalogErrors.WorkConditionTypeNotFound);
                }

                workConditionTypeCatalogItemId = workConditionTypeLookup.InternalId;
            }

            var catalogResolution = await ResolveCatalogReferenceAsync(
                tenantId,
                JobCatalogCategory.WorkingCondition,
                input.CatalogItemId,
                input.CatalogCode,
                input.CatalogName,
                allowInlineCatalogCreate,
                authorizationService,
                catalogRepository,
                createdCatalogItems,
                categoryInvalidation,
                cancellationToken);
            if (catalogResolution.IsFailure)
            {
                return Result<JobProfileMutation>.Failure(catalogResolution.Error);
            }

            var name = string.IsNullOrWhiteSpace(input.Name)
                ? catalogResolution.Value?.Name ?? string.Empty
                : input.Name;

            workingConditionEntities.Add(JobProfileWorkingCondition.Create(
                workConditionTypeCatalogItemId,
                catalogResolution.Value?.Id,
                catalogResolution.Value,
                name,
                input.Notes,
                input.SortOrder));
        }

        var dependentInternalIds = new List<long>();
        var dependentEntities = new List<JobProfileDependentPosition>();
        foreach (var input in dependentPositions)
        {
            var dependentInternalId = await profileRepository.ResolveProfileIdAsync(tenantId, input.DependentJobProfileId, cancellationToken);
            if (!dependentInternalId.HasValue)
            {
                return Result<JobProfileMutation>.Failure(
                    await profileRepository.ExistsOutsideTenantAsync(input.DependentJobProfileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : JobProfileErrors.JobProfileNotFound);
            }

            if (profilePublicId.HasValue && input.DependentJobProfileId == profilePublicId.Value)
            {
                return Result<JobProfileMutation>.Failure(JobProfileErrors.DependencyCycle);
            }

            dependentInternalIds.Add(dependentInternalId.Value);
            dependentEntities.Add(JobProfileDependentPosition.Create(dependentInternalId.Value, input.Quantity, input.Notes));
        }

        if (profileInternalId.HasValue)
        {
            var graph = await profileRepository.GetDependencyGraphAsync(tenantId, cancellationToken);
            if (JobProfileDependencyAnalyzer.WouldCreateDependentCycle(profileInternalId.Value, dependentInternalIds, graph))
            {
                return Result<JobProfileMutation>.Failure(JobProfileErrors.DependencyCycle);
            }
        }

        return Result<JobProfileMutation>.Success(
            new JobProfileMutation(
                requirementEntities,
                functionEntities,
                relationEntities,
                competencyEntities,
                trainingEntities,
                benefitEntities,
                workingConditionEntities,
                dependentEntities));
    }

    private static async Task<Result<string>> ResolveRequirementDescriptionAsync(
        JobRequirementType requirementType,
        string description,
        Guid actorUserPublicId,
        IInternalCatalogRepository internalCatalogRepository,
        IDateTimeProvider dateTimeProvider,
        IList<InternalCatalogValue> createdInternalCatalogValues,
        CancellationToken cancellationToken)
    {
        if (!InternalCatalogRegistry.TryGetRequirementDefinition(requirementType, out var definition) ||
            definition.RenderType != InternalCatalogRenderType.Search ||
            string.IsNullOrWhiteSpace(definition.CatalogKey) ||
            string.IsNullOrWhiteSpace(description))
        {
            return Result<string>.Success(description);
        }

        var normalizedDescription = InternalCatalogValue.InternalCatalogNormalization.NormalizeValue(description);
        var pendingCreatedValue = createdInternalCatalogValues.FirstOrDefault(
            value => value.CatalogKey == definition.CatalogKey &&
                     value.NormalizedValue == normalizedDescription);
        if (pendingCreatedValue is not null)
        {
            pendingCreatedValue.RegisterUsage(dateTimeProvider.UtcNow);
            return Result<string>.Success(pendingCreatedValue.Value);
        }

        var resolution = await InternalCatalogValueResolver.ResolveForUsageAsync(
            definition.CatalogKey,
            description,
            actorUserPublicId,
            internalCatalogRepository,
            dateTimeProvider,
            cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<string>.Failure(resolution.Error);
        }

        if (resolution.Value.CreatedValue is not null)
        {
            internalCatalogRepository.Add(resolution.Value.CreatedValue);
            createdInternalCatalogValues.Add(resolution.Value.CreatedValue);
        }

        return Result<string>.Success(resolution.Value.ResolvedValue);
    }

    private static async Task<Result<JobCatalogItem?>> ResolveCatalogReferenceAsync(
        Guid tenantId,
        JobCatalogCategory? category,
        Guid? catalogItemId,
        string? inlineCode,
        string? inlineName,
        bool allowInlineCatalogCreate,
        IJobProfileAuthorizationService authorizationService,
        IJobCatalogRepository catalogRepository,
        IList<JobCatalogItem> createdCatalogItems,
        ISet<JobCatalogCategory> categoryInvalidation,
        CancellationToken cancellationToken)
    {
        if (!category.HasValue)
        {
            return Result<JobCatalogItem?>.Success(null);
        }

        if (catalogItemId.HasValue)
        {
            var existing = await catalogRepository.ResolveActiveItemAsync(tenantId, category.Value, catalogItemId.Value, cancellationToken);
            if (existing is not null)
            {
                return Result<JobCatalogItem?>.Success(existing);
            }

            return Result<JobCatalogItem?>.Failure(
                await catalogRepository.ExistsOutsideTenantAsync(catalogItemId.Value, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.CatalogItemNotFound);
        }

        if (string.IsNullOrWhiteSpace(inlineCode) && string.IsNullOrWhiteSpace(inlineName))
        {
            return Result<JobCatalogItem?>.Success(null);
        }

        if (!allowInlineCatalogCreate)
        {
            return Result<JobCatalogItem?>.Failure(JobProfileErrors.InlineCatalogCreateForbidden);
        }

        if (string.IsNullOrWhiteSpace(inlineCode) || string.IsNullOrWhiteSpace(inlineName))
        {
            return Result<JobCatalogItem?>.Failure(JobProfileErrors.CatalogItemNotFound);
        }

        var normalizedName = inlineName.Trim().ToUpperInvariant();
        var byName = await catalogRepository.FindActiveByNameAsync(tenantId, category.Value, normalizedName, cancellationToken);
        if (byName is not null)
        {
            return Result<JobCatalogItem?>.Success(byName);
        }

        var normalizedCode = inlineCode.Trim().ToUpperInvariant();
        if (await catalogRepository.CodeExistsAsync(tenantId, category.Value, normalizedCode, excludingItemId: null, cancellationToken))
        {
            return Result<JobCatalogItem?>.Failure(JobProfileErrors.CatalogCodeConflict);
        }

        var created = JobCatalogItem.Create(category.Value, inlineCode, inlineName);
        created.SetTenantId(tenantId);

        catalogRepository.Add(created);
        createdCatalogItems.Add(created);
        _ = categoryInvalidation.Add(category.Value);

        return Result<JobCatalogItem?>.Success(created);
    }

    private static JobCatalogCategory? ResolveRequirementCategory(JobRequirementType requirementType) =>
        requirementType switch
        {
            JobRequirementType.Education => JobCatalogCategory.EducationLevel,
            JobRequirementType.Knowledge => JobCatalogCategory.KnowledgeArea,
            JobRequirementType.Certification => JobCatalogCategory.Training,
            _ => null
        };

    private static bool HasInlineCatalogReferences(
        IReadOnlyCollection<JobProfileRequirementInput> requirements,
        IReadOnlyCollection<JobProfileRelationInput> relations,
        IReadOnlyCollection<JobProfileCompetencyInput> competencies,
        IReadOnlyCollection<JobProfileTrainingInput> trainings,
        IReadOnlyCollection<JobProfileBenefitInput> benefits,
        IReadOnlyCollection<JobProfileWorkingConditionInput> workingConditions) =>
        requirements.Any(static item => !item.CatalogItemId.HasValue && (!string.IsNullOrWhiteSpace(item.CatalogCode) || !string.IsNullOrWhiteSpace(item.CatalogName))) ||
        relations.Any(static item => !item.CatalogItemId.HasValue && (!string.IsNullOrWhiteSpace(item.CatalogCode) || !string.IsNullOrWhiteSpace(item.CatalogName))) ||
        competencies.Any(static item => !item.CatalogItemId.HasValue && (!string.IsNullOrWhiteSpace(item.CatalogCode) || !string.IsNullOrWhiteSpace(item.CatalogName))) ||
        trainings.Any(static item => !item.CatalogItemId.HasValue && (!string.IsNullOrWhiteSpace(item.CatalogCode) || !string.IsNullOrWhiteSpace(item.CatalogName))) ||
        benefits.Any(static item => !item.CatalogItemId.HasValue && (!string.IsNullOrWhiteSpace(item.CatalogCode) || !string.IsNullOrWhiteSpace(item.CatalogName))) ||
        workingConditions.Any(static item => !item.CatalogItemId.HasValue && (!string.IsNullOrWhiteSpace(item.CatalogCode) || !string.IsNullOrWhiteSpace(item.CatalogName)));
}

internal static class JobProfileDependencyAnalyzer
{
    public static bool WouldCreateReportsToCycle(
        long sourceInternalId,
        long candidateReportsToInternalId,
        IReadOnlyDictionary<long, JobProfileDependencyNodeData> byInternalId)
    {
        var cursor = candidateReportsToInternalId;
        var visited = new HashSet<long>();

        while (true)
        {
            if (cursor == sourceInternalId)
            {
                return true;
            }

            if (!visited.Add(cursor))
            {
                return true;
            }

            if (!byInternalId.TryGetValue(cursor, out var node) || !node.ReportsToInternalId.HasValue)
            {
                return false;
            }

            cursor = node.ReportsToInternalId.Value;
        }
    }

    public static bool WouldCreateDependentCycle(
        long sourceInternalId,
        IReadOnlyCollection<long> candidateDependentInternalIds,
        IReadOnlyCollection<JobProfileDependencyNodeData> graph)
    {
        var adjacency = graph.ToDictionary(
            static node => node.InternalId,
            static node => node.DependentProfileInternalIds.Distinct().ToArray());

        adjacency[sourceInternalId] = candidateDependentInternalIds.Distinct().ToArray();

        var pending = new Stack<long>(adjacency[sourceInternalId]);
        var visited = new HashSet<long>();

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current == sourceInternalId)
            {
                return true;
            }

            if (!visited.Add(current))
            {
                continue;
            }

            if (!adjacency.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var child in next)
            {
                pending.Push(child);
            }
        }

        return false;
    }
}

internal static class JobProfilePolicyAdapter
{
    public static JobProfileListItemResponse ApplyAllowedActions(
        JobProfileListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManageProfiles)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                JobProfilePermissionCodes.ResourceKey,
                response.Status.ToString(),
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManageProfiles,
                SupportsDelete: false,
                SupportsArchive: true,
                ArchiveAllowed: canManageProfiles,
                SupportsActivate: false,
                SupportsInactivate: false,
                NonEditableStates: [JobProfileStatus.Archived.ToString()]));

        return response with { AllowedActions = allowedActions };
    }

    public static JobProfileResponse ApplyAllowedActions(
        JobProfileResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManageProfiles)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                JobProfilePermissionCodes.ResourceKey,
                response.Status.ToString(),
                response.IsActive,
                HasDependencies: response.DependentPositions.Count > 0,
                SupportsEdit: true,
                EditAllowed: canManageProfiles,
                SupportsDelete: false,
                SupportsArchive: true,
                ArchiveAllowed: canManageProfiles,
                SupportsActivate: false,
                SupportsInactivate: false,
                NonEditableStates: [JobProfileStatus.Archived.ToString()]));

        return response with { AllowedActions = allowedActions };
    }
    public static JobProfileCoreResponse ApplyAllowedActions(
        JobProfileCoreResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManageProfiles)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                JobProfilePermissionCodes.ResourceKey,
                response.Status.ToString(),
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManageProfiles,
                SupportsDelete: false,
                SupportsArchive: true,
                ArchiveAllowed: canManageProfiles,
                SupportsActivate: false,
                SupportsInactivate: false,
                NonEditableStates: [JobProfileStatus.Archived.ToString()]));

        return response with { AllowedActions = allowedActions };
    }

    public static JobProfileEntityResponse ApplyAllowedActions(
        JobProfileEntityResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManageProfiles)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                JobProfilePermissionCodes.ResourceKey,
                response.Status.ToString(),
                response.IsActive,
                SupportsEdit: true,
                EditAllowed: canManageProfiles,
                SupportsDelete: false,
                SupportsArchive: true,
                ArchiveAllowed: canManageProfiles,
                SupportsActivate: false,
                SupportsInactivate: false,
                NonEditableStates: [JobProfileStatus.Archived.ToString()]));

        return response with { AllowedActions = allowedActions };
    }
}

internal static class JobProfileCommandSupport
{
    public static bool MeetsPublishedMinimumRequirements(
        string? objective,
        string? responsibilities,
        IReadOnlyCollection<JobProfileRequirement> requirements,
        IReadOnlyCollection<JobProfileFunction> functions) =>
        !string.IsNullOrWhiteSpace(objective) &&
        !string.IsNullOrWhiteSpace(responsibilities) &&
        requirements.Count > 0 &&
        functions.Count > 0;

    public static bool HasReportsToAlsoAsDependentPosition(
        long? reportsToInternalId,
        IReadOnlyCollection<JobProfileDependentPosition> dependentPositions) =>
        reportsToInternalId.HasValue &&
        dependentPositions.Any(item => item.DependentJobProfileId == reportsToInternalId.Value);

    public static async Task<Result<long>> ResolveOrgUnitInternalIdAsync(
        Guid tenantId,
        Guid orgUnitId,
        IJobProfileAuthorizationService authorizationService,
        IJobProfileRepository repository,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        var orgUnitInternalId = await repository.ResolveOrgUnitIdAsync(tenantId, orgUnitId, cancellationToken);
        if (orgUnitInternalId.HasValue)
        {
            return Result<long>.Success(orgUnitInternalId.Value);
        }

        return Result<long>.Failure(
            await repository.OrgUnitExistsOutsideTenantAsync(orgUnitId, cancellationToken)
                ? authorizationService.TenantMismatch(action)
                : JobProfileErrors.OrgUnitNotFound);
    }

    public static async Task<Result<long?>> ResolveReportsToInternalIdAsync(
        Guid tenantId,
        Guid? reportsToProfileId,
        Guid? sourceProfilePublicId,
        long? sourceInternalId,
        IJobProfileAuthorizationService authorizationService,
        IJobProfileRepository repository,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (!reportsToProfileId.HasValue)
        {
            return Result<long?>.Success(null);
        }

        if (sourceProfilePublicId.HasValue && sourceProfilePublicId.Value == reportsToProfileId.Value)
        {
            return Result<long?>.Failure(JobProfileErrors.DependencyCycle);
        }

        var reportsToInternalId = await repository.ResolveProfileIdAsync(tenantId, reportsToProfileId.Value, cancellationToken);
        if (!reportsToInternalId.HasValue)
        {
            return Result<long?>.Failure(
                await repository.ExistsOutsideTenantAsync(reportsToProfileId.Value, cancellationToken)
                    ? authorizationService.TenantMismatch(action)
                    : JobProfileErrors.ReportsToProfileNotFound);
        }

        if (sourceInternalId.HasValue)
        {
            var graph = await repository.GetDependencyGraphAsync(tenantId, cancellationToken);
            var byInternalId = graph.ToDictionary(static node => node.InternalId);

            if (JobProfileDependencyAnalyzer.WouldCreateReportsToCycle(
                    sourceInternalId.Value,
                    reportsToInternalId.Value,
                    byInternalId))
            {
                return Result<long?>.Failure(JobProfileErrors.DependencyCycle);
            }
        }

        return Result<long?>.Success(reportsToInternalId.Value);
    }

    public static async Task<Result<long?>> ResolvePositionCategoryInternalIdAsync(
        Guid tenantId,
        Guid? positionCategoryId,
        IPositionCatalogLookup repository,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (!positionCategoryId.HasValue)
        {
            return Result<long?>.Success(null);
        }

        var categoryInternalId = await repository.ResolvePositionCategoryIdAsync(tenantId, positionCategoryId.Value, cancellationToken);
        if (categoryInternalId.HasValue)
        {
            return Result<long?>.Success(categoryInternalId.Value);
        }

        return Result<long?>.Failure(
            await repository.ExistsCategoryOutsideTenantAsync(positionCategoryId.Value, cancellationToken)
                ? PositionDescriptionCatalogErrors.TenantMismatch(action)
                : PositionDescriptionCatalogErrors.CategoryNotFound);
    }

    public static async Task<Result<long?>> ResolvePositionDescriptionCatalogItemInternalIdAsync(
        Guid tenantId,
        Guid? catalogItemId,
        PositionDescriptionCatalogType catalogType,
        Error notFoundError,
        IPositionCatalogLookup repository,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (!catalogItemId.HasValue)
        {
            return Result<long?>.Success(null);
        }

        var lookup = await repository.GetActiveCatalogReferenceAsync(
            tenantId,
            catalogType,
            catalogItemId.Value,
            cancellationToken);
        if (lookup is not null)
        {
            return Result<long?>.Success(lookup.InternalId);
        }

        return Result<long?>.Failure(
            await repository.ExistsCatalogItemOutsideTenantAsync(catalogItemId.Value, cancellationToken)
                ? PositionDescriptionCatalogErrors.TenantMismatch(action)
                : notFoundError);
    }

    public static async Task<Result<bool>> ResolveInlineCatalogPermissionAsync(
        bool allowInlineCatalogCreate,
        bool inlineCatalogRequested,
        Guid tenantId,
        IJobProfileAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        if (!inlineCatalogRequested)
        {
            return Result<bool>.Success(false);
        }

        if (!allowInlineCatalogCreate)
        {
            return Result<bool>.Failure(JobProfileErrors.InlineCatalogCreateForbidden);
        }

        var catalogAuthorization = await authorizationService.EnsureCanManageCatalogsAsync(tenantId, cancellationToken);
        if (catalogAuthorization.IsFailure)
        {
            return Result<bool>.Failure(
                catalogAuthorization.Error.Type == ErrorType.Forbidden
                    ? JobProfileErrors.InlineCatalogCreateForbidden
                    : catalogAuthorization.Error);
        }

        return Result<bool>.Success(true);
    }

    public static async Task WriteInlineCatalogAuditAsync(
        Guid tenantId,
        IReadOnlyCollection<JobCatalogItem> createdCatalogItems,
        IReadOnlySet<JobCatalogCategory> categories,
        IAuditService auditService,
        IJobCatalogRepository catalogRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (createdCatalogItems.Count == 0)
        {
            return;
        }

        foreach (var item in createdCatalogItems)
        {
            var response = await catalogRepository.GetResponseByIdAsync(item.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Catalog item response could not be resolved after inline creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobCatalogItemCreated,
                    AuditEntityTypes.JobCatalogItem,
                    item.PublicId,
                    item.Code,
                    AuditActions.Create,
                    $"Created job catalog item {item.Code} ({item.Category}) inline.",
                    After: response),
                cancellationToken);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var category in categories)
        {
            catalogRepository.InvalidateCategoryCache(tenantId, category);
        }
    }

    public static async Task WriteInternalCatalogAuditAsync(
        IReadOnlyCollection<InternalCatalogValue> createdValues,
        IPlatformAuditService platformAuditService,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (createdValues.Count == 0)
        {
            return;
        }

        foreach (var value in createdValues)
        {
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.InternalCatalogValueCreated,
                    AuditEntityTypes.InternalCatalogValue,
                    value.PublicId,
                    value.CatalogKey,
                    AuditActions.Create,
                    $"Created internal catalog value '{value.Value}' in '{value.CatalogKey}' from job profile flow.",
                    After: InternalCatalogResponseMapper.MapSuggestion(value, scoreOverride: 1d)),
                cancellationToken);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
