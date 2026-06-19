using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.CompetencyFramework;

public sealed record JobProfileCompetencyMatrixItemConductResponse(
    Guid ConductId,
    string Description,
    int SortOrder);

public sealed record JobProfileCompetencyMatrixItemResponse(
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
    IReadOnlyCollection<JobProfileCompetencyMatrixItemConductResponse> Conducts);

public sealed record JobProfileCompetencyMatrixResponse(
    Guid JobProfileId,
    string JobProfileCode,
    string JobProfileTitle,
    JobProfileStatus JobProfileStatus,
    int JobProfileVersion,
    Guid ConcurrencyToken,
    IReadOnlyCollection<JobProfileCompetencyMatrixItemResponse> Items,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record JobProfileCompetencyMatrixExportRow(
    Guid JobProfileId,
    string JobProfileCode,
    string JobProfileTitle,
    string JobProfileStatus,
    int JobProfileVersion,
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
    Guid? ConductId,
    string? ConductDescription,
    int? ConductSortOrder,
    string? ExpectedEvidence,
    int ItemSortOrder);

public sealed record JobProfileCompetencyMatrixItemInput(
    Guid OccupationalPyramidLevelId,
    IReadOnlyCollection<Guid> ConductIds,
    string? ExpectedEvidence,
    int SortOrder);

public sealed record GetJobProfileCompetencyMatrixQuery(Guid JobProfileId)
    : IQuery<JobProfileCompetencyMatrixResponse>;

public sealed record ExportJobProfileCompetencyMatrixQuery(Guid JobProfileId, int? MaxRows = null)
    : IQuery<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>;

public sealed record UpdateJobProfileCompetencyMatrixCommand(
    Guid JobProfileId,
    IReadOnlyCollection<JobProfileCompetencyMatrixItemInput> Items,
    Guid ConcurrencyToken)
    : ICommand<JobProfileCompetencyMatrixResponse>;

internal sealed class GetJobProfileCompetencyMatrixQueryValidator : AbstractValidator<GetJobProfileCompetencyMatrixQuery>
{
    public GetJobProfileCompetencyMatrixQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class ExportJobProfileCompetencyMatrixQueryValidator : AbstractValidator<ExportJobProfileCompetencyMatrixQuery>
{
    public ExportJobProfileCompetencyMatrixQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class UpdateJobProfileCompetencyMatrixCommandValidator : AbstractValidator<UpdateJobProfileCompetencyMatrixCommand>
{
    public UpdateJobProfileCompetencyMatrixCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Items)
            .Must(static items => items is null || items.Count <= CompetencyFrameworkValidationRules.MaxMatrixItems)
            .WithMessage("A maximum of 200 competency matrix items is allowed.");
        RuleForEach(command => command.Items).SetValidator(new JobProfileCompetencyMatrixItemInputValidator());
    }
}

internal sealed class JobProfileCompetencyMatrixItemInputValidator : AbstractValidator<JobProfileCompetencyMatrixItemInput>
{
    public JobProfileCompetencyMatrixItemInputValidator()
    {
        RuleFor(item => item.OccupationalPyramidLevelId).NotEmpty();
        RuleFor(item => item.ExpectedEvidence).MaximumLength(1000);
        RuleFor(item => item.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(item => item.ConductIds)
            .NotEmpty()
            .WithMessage("At least one competency conduct is required per matrix item.")
            .Must(static conductIds => conductIds is null || conductIds.Count <= CompetencyFrameworkValidationRules.MaxConductsPerMatrixItem)
            .WithMessage("A maximum of 50 conducts per competency matrix item is allowed.");
        RuleForEach(item => item.ConductIds).NotEqual(Guid.Empty);
    }
}

internal sealed class GetJobProfileCompetencyMatrixQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetJobProfileCompetencyMatrixQuery, JobProfileCompetencyMatrixResponse>
{
    public async Task<Result<JobProfileCompetencyMatrixResponse>> Handle(
        GetJobProfileCompetencyMatrixQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCompetencyMatrixResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetJobProfileCompetencyMatrixResponseAsync(query.JobProfileId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = CompetencyFrameworkPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
            return Result<JobProfileCompetencyMatrixResponse>.Success(response);
        }

        return Result<JobProfileCompetencyMatrixResponse>.Failure(
            await repository.JobProfileExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CompetencyFrameworkErrors.JobProfileNotFound);
    }
}

internal sealed class ExportJobProfileCompetencyMatrixQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<ExportJobProfileCompetencyMatrixQuery, IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>
{
    public async Task<Result<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>> Handle(
        ExportJobProfileCompetencyMatrixQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await repository.GetJobProfileCompetencyMatrixExportRowsAsync(query.JobProfileId, query.MaxRows, cancellationToken);
        if (rows.Count > 0)
        {
            return Result<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>.Success(rows);
        }

        var matrix = await repository.GetJobProfileCompetencyMatrixResponseAsync(query.JobProfileId, cancellationToken);
        if (matrix is not null)
        {
            return Result<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>.Success(rows);
        }

        return Result<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>.Failure(
            await repository.JobProfileExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CompetencyFrameworkErrors.JobProfileNotFound);
    }
}

internal sealed class UpdateJobProfileCompetencyMatrixCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateJobProfileCompetencyMatrixCommand, JobProfileCompetencyMatrixResponse>
{
    public async Task<Result<JobProfileCompetencyMatrixResponse>> Handle(
        UpdateJobProfileCompetencyMatrixCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCompetencyMatrixResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetJobProfileAggregateByIdAsync(command.JobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfileCompetencyMatrixResponse>.Failure(
                await repository.JobProfileExistsOutsideTenantAsync(command.JobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.JobProfileNotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileCompetencyMatrixResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        if (profile.Status == JobProfileStatus.Archived)
        {
            return Result<JobProfileCompetencyMatrixResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
        }

        var before = await repository.GetJobProfileCompetencyMatrixResponseAsync(profile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile competency matrix response could not be resolved before update.");

        // Batch-resolve the referenced pyramid levels and conducts up front (one query per category) so
        // the per-item loop below resolves from memory instead of an N+1 fan-out. The competency / type /
        // behavior-level triple is no longer supplied by the client: it is derived from each item's
        // conducts (every conduct already carries its own triple), so nothing else needs resolving here.
        var levelsById = await repository.ResolveActiveOccupationalPyramidLevelsAsync(
            profile.TenantId,
            command.Items.Select(item => item.OccupationalPyramidLevelId).ToArray(),
            cancellationToken);
        var conductsById = await repository.ResolveActiveCompetencyConductsAsync(
            profile.TenantId,
            command.Items.SelectMany(item => item.ConductIds).ToArray(),
            cancellationToken);

        var matrixItems = new List<JobProfileCompetencyExpectation>();
        var uniqueCombinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in command.Items)
        {
            var levelResolution = await CompetencyFrameworkCatalogResolver.ResolvePyramidLevelFromMapAsync(
                levelsById,
                item.OccupationalPyramidLevelId,
                repository,
                authorizationService,
                RbacPermissionAction.Update,
                cancellationToken);
            if (levelResolution.IsFailure)
            {
                return Result<JobProfileCompetencyMatrixResponse>.Failure(levelResolution.Error);
            }

            // Derive the item's competency / type / behavior-level triple from its conducts. A matrix
            // item is a single cell of the matrix, so every conduct it lists must share one triple;
            // a divergent conduct is a matrix-constraint conflict. The validator guarantees >= 1 conduct.
            var conducts = new List<JobProfileCompetencyExpectationConduct>();
            var conductSet = new HashSet<Guid>();
            var conductSort = 0;
            long competencyCatalogItemId = 0;
            long competencyTypeCatalogItemId = 0;
            long behaviorLevelCatalogItemId = 0;

            foreach (var conductId in item.ConductIds)
            {
                if (!conductSet.Add(conductId))
                {
                    return Result<JobProfileCompetencyMatrixResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
                }

                var conductResolution = await CompetencyFrameworkCatalogResolver.ResolveConductFromMapAsync(
                    conductsById,
                    conductId,
                    repository,
                    authorizationService,
                    RbacPermissionAction.Update,
                    cancellationToken);
                if (conductResolution.IsFailure)
                {
                    return Result<JobProfileCompetencyMatrixResponse>.Failure(conductResolution.Error);
                }

                var conduct = conductResolution.Value;
                if (conductSet.Count == 1)
                {
                    competencyCatalogItemId = conduct.CompetencyCatalogItemId;
                    competencyTypeCatalogItemId = conduct.CompetencyTypeCatalogItemId;
                    behaviorLevelCatalogItemId = conduct.BehaviorLevelCatalogItemId;
                }
                else if (conduct.CompetencyCatalogItemId != competencyCatalogItemId ||
                    conduct.CompetencyTypeCatalogItemId != competencyTypeCatalogItemId ||
                    conduct.BehaviorLevelCatalogItemId != behaviorLevelCatalogItemId)
                {
                    return Result<JobProfileCompetencyMatrixResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
                }

                var link = JobProfileCompetencyExpectationConduct.Create(conduct.Id, conductSort++);
                link.SetTenantId(profile.TenantId);
                conducts.Add(link);
            }

            var uniqueKey = $"{levelResolution.Value.Id}:{competencyCatalogItemId}:{competencyTypeCatalogItemId}:{behaviorLevelCatalogItemId}";
            if (!uniqueCombinations.Add(uniqueKey))
            {
                return Result<JobProfileCompetencyMatrixResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
            }

            var expectation = JobProfileCompetencyExpectation.Create(
                profile.Id,
                levelResolution.Value.Id,
                competencyCatalogItemId,
                competencyTypeCatalogItemId,
                behaviorLevelCatalogItemId,
                item.ExpectedEvidence,
                item.SortOrder);
            expectation.SetTenantId(profile.TenantId);
            expectation.ReplaceConducts(conducts);
            matrixItems.Add(expectation);
        }

        var previous = await repository.GetExpectationsByJobProfileIdAsync(profile.Id, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.RemoveExpectations(previous);
            repository.AddExpectations(matrixItems);

            profile.UpdateCore(
                profile.Code,
                profile.Title,
                profile.Objective,
                profile.OrgUnitId,
                profile.ReportsToJobProfileId,
                profile.PositionCategoryId,
                profile.StrategicObjectiveCatalogItemId,
                profile.AssignedWorkEquipmentCatalogItemId,
                profile.ResponsibilityCatalogItemId,
                profile.DecisionScope,
                profile.AssignedResources,
                profile.Responsibilities,
                profile.MarketSalaryReference,
                profile.ValuationNotes,
                profile.EffectiveFromUtc,
                profile.EffectiveToUtc,
                bumpVersion: true);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetJobProfileCompetencyMatrixResponseAsync(profile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile competency matrix response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileCompetencyMatrixUpdated,
                    AuditEntityTypes.JobProfileCompetencyMatrix,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated competency matrix for job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCompetencyMatrixResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
