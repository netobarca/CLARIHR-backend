using System.Globalization;
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
using FluentValidation;

namespace CLARIHR.Application.Features.JobProfiles;

public sealed record JobProfileRequirementResponse(
    Guid Id,
    Guid? CatalogItemId,
    Guid? RequirementTypeCatalogItemId,
    JobRequirementType RequirementType,
    string Description,
    int SortOrder);

public sealed record JobProfileFunctionResponse(
    Guid Id,
    JobFunctionType FunctionType,
    Guid? FrequencyCatalogItemId,
    string Description,
    int SortOrder);

public sealed record JobProfileRelationResponse(
    Guid Id,
    Guid? CatalogItemId,
    JobRelationType RelationType,
    string Counterpart,
    string? Notes,
    int SortOrder);

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
    Guid Id,
    Guid? CatalogItemId,
    string Name,
    string? Notes,
    int SortOrder);

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
    Guid Id,
    Guid? CatalogItemId,
    string Name,
    string? Notes,
    int SortOrder);

public sealed record JobProfileWorkingConditionResponse(
    Guid Id,
    Guid? CatalogItemId,
    Guid? WorkConditionTypeCatalogItemId,
    string Name,
    string? Notes,
    int SortOrder);

public sealed record JobProfileDependentPositionResponse(
    Guid Id,
    Guid DependentJobProfileId,
    string DependentJobProfileCode,
    string DependentJobProfileTitle,
    int Quantity,
    string? Notes);

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

public sealed record JobProfileVacancyTemplateResponse(
    Guid Id,
    string Code,
    string Title,
    string? Objective,
    string? Responsibilities,
    string? WorkingConditionSummary,
    string? BenefitsSummary,
    IReadOnlyCollection<JobProfileRequirementResponse> Requirements,
    IReadOnlyCollection<JobProfileFunctionResponse> Functions,
    IReadOnlyCollection<JobProfileCompetencyResponse> Competencies,
    IReadOnlyCollection<JobProfileTrainingResponse> Trainings);

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

public sealed record JobProfileCompensationInput(
    Guid? SalaryTabulatorLineId,
    Guid? SalaryClassId,
    string? SalaryClassCode,
    string? CurrencyCode,
    decimal? MinAmount,
    decimal? MaxAmount);

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

public sealed record GetJobProfileByIdQuery(Guid JobProfileId) : IQuery<JobProfileResponse>;

public sealed record GetJobProfileCoreByIdQuery(Guid JobProfileId) : IQuery<JobProfileCoreResponse>;

public sealed record GetJobProfileVacancyTemplateQuery(Guid JobProfileId) : IQuery<JobProfileVacancyTemplateResponse>;

public sealed record GetJobProfilePrintQuery(Guid JobProfileId) : IQuery<JobProfilePrintResponse>;

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
    bool AllowInlineCatalogCreate,
    JobProfileCompensationInput? Compensation) : ICommand<JobProfileCoreResponse>;

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
    JobProfileCompensationInput? Compensation,
    Guid ConcurrencyToken) : ICommand<JobProfileCoreResponse>;

public sealed record PublishJobProfileCommand(Guid JobProfileId, Guid ConcurrencyToken) : ICommand<JobProfileResponse>;

public sealed record ArchiveJobProfileCommand(Guid JobProfileId, Guid ConcurrencyToken) : ICommand<JobProfileResponse>;

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

internal sealed class GetJobProfileVacancyTemplateQueryValidator : AbstractValidator<GetJobProfileVacancyTemplateQuery>
{
    public GetJobProfileVacancyTemplateQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class GetJobProfilePrintQueryValidator : AbstractValidator<GetJobProfilePrintQuery>
{
    public GetJobProfilePrintQueryValidator()
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

        ApplyCollectionRules();
    }

    private void ApplyCollectionRules()
    {
        RuleFor(command => command.Compensation)
            .SetValidator(new JobProfileCompensationInputValidator()!);
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

        ApplyCollectionRules();
    }

    private void ApplyCollectionRules()
    {
        RuleFor(command => command.Compensation)
            .SetValidator(new JobProfileCompensationInputValidator()!);
    }
}

internal sealed class PublishJobProfileCommandValidator : AbstractValidator<PublishJobProfileCommand>
{
    public PublishJobProfileCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ArchiveJobProfileCommandValidator : AbstractValidator<ArchiveJobProfileCommand>
{
    public ArchiveJobProfileCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
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

internal sealed class JobProfileCompensationInputValidator : AbstractValidator<JobProfileCompensationInput>
{
    public JobProfileCompensationInputValidator()
    {
        RuleFor(input => input)
            .Must(static input =>
                input.SalaryTabulatorLineId.HasValue ||
                input.SalaryClassId.HasValue ||
                !string.IsNullOrWhiteSpace(input.SalaryClassCode))
            .WithMessage("A salary tabulator line or salary class reference is required for compensation.");
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
    : IQueryHandler<GetJobProfileByIdQuery, JobProfileResponse>
{
    public async Task<Result<JobProfileResponse>> Handle(GetJobProfileByIdQuery query, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.JobProfileId, cancellationToken);
        if (response is not null)
        {
            var canManageProfiles = (await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = JobProfilePolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManageProfiles);
            return Result<JobProfileResponse>.Success(response);
        }

        return Result<JobProfileResponse>.Failure(
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

internal sealed class GetJobProfileVacancyTemplateQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileVacancyTemplateQuery, JobProfileVacancyTemplateResponse>
{
    public async Task<Result<JobProfileVacancyTemplateResponse>> Handle(
        GetJobProfileVacancyTemplateQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileVacancyTemplateResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileVacancyTemplateResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetVacancyTemplateByIdAsync(query.JobProfileId, cancellationToken);
        if (response is not null)
        {
            return Result<JobProfileVacancyTemplateResponse>.Success(response);
        }

        return Result<JobProfileVacancyTemplateResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : JobProfileErrors.JobProfileNotFound);
    }
}

internal sealed class GetJobProfilePrintQueryHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetJobProfilePrintQuery, JobProfilePrintResponse>
{
    public async Task<Result<JobProfilePrintResponse>> Handle(
        GetJobProfilePrintQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfilePrintResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfilePrintResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetPrintByIdAsync(query.JobProfileId, cancellationToken);
        if (response is not null)
        {
            var canManageProfiles = (await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            var enrichedProfile = JobProfilePolicyAdapter.ApplyAllowedActions(response.Profile, resourceActionPolicyService, canManageProfiles);
            response = response with { Profile = enrichedProfile };
            return Result<JobProfilePrintResponse>.Success(response);
        }

        return Result<JobProfilePrintResponse>.Failure(
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
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
    ISalaryTabulatorRepository salaryTabulatorRepository,
    IAuditService auditService,
    IPlatformAuditService platformAuditService,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
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

        if (mutation.Value.CompensationSalaryClassCatalogItemId.HasValue &&
            !string.IsNullOrWhiteSpace(mutation.Value.CompensationSalaryScaleCode))
        {
            profile.SetCompensationReference(
                mutation.Value.CompensationSalaryClassCatalogItemId.Value,
                salaryClassCatalogItem: null,
                mutation.Value.CompensationSalaryScaleCode!,
                bumpVersion: false);
        }
        else
        {
            profile.ClearCompensationReference(bumpVersion: false);
        }

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
    IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
    ISalaryTabulatorRepository salaryTabulatorRepository,
    IAuditService auditService,
    IPlatformAuditService platformAuditService,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
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

            if (mutation.Value.CompensationSalaryClassCatalogItemId.HasValue &&
                !string.IsNullOrWhiteSpace(mutation.Value.CompensationSalaryScaleCode))
            {
                profile.SetCompensationReference(
                    mutation.Value.CompensationSalaryClassCatalogItemId.Value,
                    salaryClassCatalogItem: null,
                    mutation.Value.CompensationSalaryScaleCode!,
                    bumpVersion: false);
            }
            else
            {
                profile.ClearCompensationReference(bumpVersion: false);
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

internal sealed class PublishJobProfileCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PublishJobProfileCommand, JobProfileResponse>
{
    public async Task<Result<JobProfileResponse>> Handle(PublishJobProfileCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        if (profile.Status == JobProfileStatus.Archived)
        {
            return Result<JobProfileResponse>.Failure(JobProfileErrors.StateConflict);
        }

        var before = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile response could not be resolved before publish.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                profile.Publish();
            }
            catch (InvalidOperationException)
            {
                return Result<JobProfileResponse>.Failure(JobProfileErrors.PublishRequirementsMissing);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved after publish.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfilePublished,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Published job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ArchiveJobProfileCommandHandler(
    IJobProfileAuthorizationService authorizationService,
    IJobProfileRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveJobProfileCommand, JobProfileResponse>
{
    public async Task<Result<JobProfileResponse>> Handle(ArchiveJobProfileCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageProfilesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : JobProfileErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileResponse>.Failure(JobProfileErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile response could not be resolved before archive.");

        if (profile.Status == JobProfileStatus.Archived)
        {
            return Result<JobProfileResponse>.Success(before);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.Archive();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile response could not be resolved after archive.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileArchived,
                    AuditEntityTypes.JobProfile,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Archive,
                    $"Archived job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed record JobProfileMutation(
    IReadOnlyCollection<JobProfileRequirement> Requirements,
    IReadOnlyCollection<JobProfileFunction> Functions,
    IReadOnlyCollection<JobProfileRelation> Relations,
    IReadOnlyCollection<JobProfileCompetency> Competencies,
    IReadOnlyCollection<JobProfileTraining> Trainings,
    long? CompensationSalaryClassCatalogItemId,
    string? CompensationSalaryClassCode,
    string? CompensationSalaryClassName,
    string? CompensationSalaryScaleCode,
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
        IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
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
            command.Compensation,
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
        IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
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
            command.Compensation,
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

    private static async Task<Result<JobProfileMutation>> BuildAsync(
        Guid tenantId,
        IReadOnlyCollection<JobProfileRequirementInput> requirements,
        IReadOnlyCollection<JobProfileFunctionInput> functions,
        IReadOnlyCollection<JobProfileRelationInput> relations,
        IReadOnlyCollection<JobProfileCompetencyInput> competencies,
        IReadOnlyCollection<JobProfileTrainingInput> trainings,
        JobProfileCompensationInput? compensation,
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
        IPositionDescriptionCatalogRepository positionDescriptionCatalogRepository,
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

        long? compensationSalaryClassCatalogItemId = null;
        string? compensationSalaryClassCode = null;
        string? compensationSalaryClassName = null;
        string? compensationSalaryScaleCode = null;

        if (compensation is not null)
        {
            var action = profilePublicId.HasValue ? RbacPermissionAction.Update : RbacPermissionAction.Create;
            var effectiveRangeStartUtc = (effectiveFromUtc ?? dateTimeProvider.UtcNow).Date;
            var effectiveRangeEndUtc = effectiveToUtc?.Date;
            SalaryTabulatorLine? salaryLine = null;

            if (compensation.SalaryTabulatorLineId.HasValue)
            {
                salaryLine = await salaryTabulatorRepository.GetLineByIdAsync(
                    compensation.SalaryTabulatorLineId.Value,
                    cancellationToken);
                if (salaryLine is null)
                {
                    return Result<JobProfileMutation>.Failure(
                        await salaryTabulatorRepository.LineExistsOutsideTenantAsync(compensation.SalaryTabulatorLineId.Value, cancellationToken)
                            ? SalaryTabulatorErrors.TenantMismatch(action)
                            : JobProfileErrors.CompensationTabulatorLineNotFound);
                }
            }
            else
            {
                string? salaryClassCode = null;
                if (compensation.SalaryClassId.HasValue)
                {
                    salaryClassCode = await positionDescriptionCatalogRepository.ResolveSalaryClassCodeByCatalogIdAsync(
                        tenantId,
                        compensation.SalaryClassId.Value,
                        cancellationToken);

                    if (string.IsNullOrWhiteSpace(salaryClassCode))
                    {
                        salaryLine = await salaryTabulatorRepository.GetLineByIdAsync(
                            compensation.SalaryClassId.Value,
                            cancellationToken);
                    }
                }

                if (salaryLine is null &&
                    string.IsNullOrWhiteSpace(salaryClassCode) &&
                    !string.IsNullOrWhiteSpace(compensation.SalaryClassCode))
                {
                    salaryClassCode = compensation.SalaryClassCode.Trim().ToUpperInvariant();
                }

                if (salaryLine is null && string.IsNullOrWhiteSpace(salaryClassCode))
                {
                    return Result<JobProfileMutation>.Failure(JobProfileErrors.CompensationTabulatorLineNotFound);
                }

                salaryLine ??= await salaryTabulatorRepository.FindActiveLineForLegacyCompensationAsync(
                    tenantId,
                    salaryClassCode!,
                    compensation.CurrencyCode,
                    compensation.MinAmount,
                    compensation.MaxAmount,
                    effectiveRangeStartUtc,
                    effectiveRangeEndUtc,
                    cancellationToken);
                if (salaryLine is null)
                {
                    return Result<JobProfileMutation>.Failure(JobProfileErrors.CompensationTabulatorLineNotFound);
                }
            }

            var lineIsEffective = salaryLine.TenantId == tenantId &&
                                  salaryLine.IsActive &&
                                  salaryLine.EffectiveFromUtc.Date <= (effectiveRangeEndUtc ?? DateTime.SpecifyKind(DateTime.MaxValue.Date, DateTimeKind.Utc)) &&
                                  (!salaryLine.EffectiveToUtc.HasValue || salaryLine.EffectiveToUtc.Value.Date >= effectiveRangeStartUtc);
            if (!lineIsEffective)
            {
                return Result<JobProfileMutation>.Failure(JobProfileErrors.CompensationTabulatorLineNotFound);
            }

            var salaryClassCatalogId = await positionDescriptionCatalogRepository.ResolveSalaryClassCatalogIdByCodeAsync(
                tenantId,
                salaryLine.SalaryClassCode,
                cancellationToken);
            if (!salaryClassCatalogId.HasValue)
            {
                return Result<JobProfileMutation>.Failure(PositionDescriptionCatalogErrors.SalaryClassNotFound);
            }

            var salaryClassLookup = await positionDescriptionCatalogRepository.GetActiveCatalogReferenceAsync(
                tenantId,
                PositionDescriptionCatalogType.SalaryClass,
                salaryClassCatalogId.Value,
                cancellationToken);
            if (salaryClassLookup is null)
            {
                return Result<JobProfileMutation>.Failure(PositionDescriptionCatalogErrors.SalaryClassNotFound);
            }

            compensationSalaryClassCatalogItemId = salaryClassLookup.InternalId;
            compensationSalaryClassCode = salaryClassLookup.Code.Trim().ToUpperInvariant();
            compensationSalaryClassName = salaryClassLookup.Name;
            compensationSalaryScaleCode = salaryLine.SalaryScaleCode;
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
            compensationSalaryClassCatalogItemId,
            compensationSalaryClassCode,
            compensationSalaryClassName,
                compensationSalaryScaleCode,
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
}

internal static class JobProfileCsvExporter
{
    public static string Export(JobProfilePrintResponse payload)
    {
        var profile = payload.Profile;
        var lines = new List<string>
        {
            "Field,Value",
            $"Id,{Escape(profile.Id.ToString())}",
            $"Code,{Escape(profile.Code)}",
            $"Title,{Escape(profile.Title)}",
            $"Status,{Escape(profile.Status.ToString())}",
            $"Version,{profile.Version}",
            $"Objective,{Escape(profile.Objective)}",
            $"Responsibilities,{Escape(profile.Responsibilities)}",
            $"OrgUnitId,{Escape(profile.OrgUnitId?.ToString())}",
            $"OrgUnitName,{Escape(profile.OrgUnitName)}",
            $"ReportsToJobProfileId,{Escape(profile.ReportsToJobProfileId?.ToString())}",
            $"ReportsToJobProfileCode,{Escape(profile.ReportsToJobProfileCode)}",
            $"ReportsToJobProfileTitle,{Escape(profile.ReportsToJobProfileTitle)}",
            $"DecisionScope,{Escape(profile.DecisionScope)}",
            $"AssignedResources,{Escape(profile.AssignedResources)}",
            $"BenefitsSummary,{Escape(profile.BenefitsSummary)}",
            $"WorkingConditionSummary,{Escape(profile.WorkingConditionSummary)}",
            $"MarketSalaryReference,{Escape(profile.MarketSalaryReference)}",
            $"ValuationNotes,{Escape(profile.ValuationNotes)}",
            $"EffectiveFromUtc,{Escape(profile.EffectiveFromUtc?.ToString("O", CultureInfo.InvariantCulture))}",
            $"EffectiveToUtc,{Escape(profile.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture))}",
            $"IsActive,{profile.IsActive}",
            $"CreatedAtUtc,{Escape(profile.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture))}",
            $"ModifiedAtUtc,{Escape(profile.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture))}",
            $"GeneratedAtUtc,{Escape(payload.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture))}"
        };

        lines.Add("Section,Type,Item,Extra1,Extra2,Extra3");

        lines.AddRange(profile.Requirements.Select(item =>
            $"Requirement,{Escape(item.RequirementType.ToString())},{Escape(item.Description)},{Escape(item.CatalogItemId?.ToString())},{item.SortOrder},"));

        lines.AddRange(profile.Functions.Select(item =>
            $"Function,{Escape(item.FunctionType.ToString())},{Escape(item.Description)},{item.SortOrder},,"));

        lines.AddRange(profile.Relations.Select(item =>
            $"Relation,{Escape(item.RelationType.ToString())},{Escape(item.Counterpart)},{Escape(item.Notes)},{Escape(item.CatalogItemId?.ToString())},{item.SortOrder}"));

        lines.AddRange(profile.Competencies.Select(item =>
            $"Competency,{Escape(item.CompetencyTypeName)},{Escape(item.CompetencyName)},{Escape(item.BehaviorLevelName)},{Escape(item.ExpectedEvidence)},{item.SortOrder}"));

        lines.AddRange(profile.Trainings.Select(item =>
            $"Training,,{Escape(item.Name)},{Escape(item.Notes)},,{item.SortOrder}"));

        if (profile.Compensation is not null)
        {
            lines.Add(
                $"Compensation,,{Escape(profile.Compensation.SalaryClassName)},{Escape(profile.Compensation.SalaryScaleCode)},{Escape(profile.Compensation.BaseAmount?.ToString(CultureInfo.InvariantCulture))},{Escape(profile.Compensation.CurrencyCode)}");
        }

        lines.AddRange(profile.Benefits.Select(item =>
            $"Benefit,,{Escape(item.Name)},{Escape(item.Notes)},,{item.SortOrder}"));

        lines.AddRange(profile.WorkingConditions.Select(item =>
            $"WorkingCondition,,{Escape(item.Name)},{Escape(item.Notes)},,{item.SortOrder}"));

        lines.AddRange(profile.DependentPositions.Select(item =>
            $"DependentPosition,,{Escape(item.DependentJobProfileTitle)},{Escape(item.DependentJobProfileCode)},{item.Quantity},{Escape(item.Notes)}"));

        return string.Join("\n", lines);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
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
        IPositionDescriptionCatalogRepository repository,
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
        IPositionDescriptionCatalogRepository repository,
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
