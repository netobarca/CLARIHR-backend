using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.CompetencyFramework;

public sealed record JobProfileCompetencyMatrixItemConductResponse(
    Guid ConductId,
    string Description,
    int SortOrder);

public sealed record JobProfileCompetencyMatrixItemResponse(
    Guid ItemPublicId,
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
    decimal? ExpectedValue,
    int SortOrder,
    IReadOnlyCollection<JobProfileCompetencyMatrixItemConductResponse> Conducts,
    Guid ConcurrencyToken,
    // PUT/PATCH on this [ResourceActions] controller return this item, so it carries allowedActions like the
    // sibling matrix response; the centralized AllowedActionsResultFilter populates it when left null.
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

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
    decimal? ExpectedValue,
    int ItemSortOrder);

public sealed record GetJobProfileCompetencyMatrixQuery(Guid JobProfileId)
    : IQuery<JobProfileCompetencyMatrixResponse>;

public sealed record GetJobProfileCompetencyMatrixItemQuery(Guid JobProfileId, Guid ItemId)
    : IQuery<JobProfileCompetencyMatrixItemResponse>;

public sealed record ExportJobProfileCompetencyMatrixQuery(Guid JobProfileId, int? MaxRows = null)
    : IQuery<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>;

public sealed record AddJobProfileCompetencyMatrixItemCommand(
    Guid JobProfileId,
    Guid OccupationalPyramidLevelId,
    IReadOnlyCollection<Guid> ConductIds,
    string? ExpectedEvidence,
    decimal? ExpectedValue,
    int SortOrder)
    : ICommand<JobProfileCompetencyMatrixItemResponse>;

public sealed record UpdateJobProfileCompetencyMatrixItemCommand(
    Guid JobProfileId,
    Guid ItemId,
    Guid OccupationalPyramidLevelId,
    IReadOnlyCollection<Guid> ConductIds,
    string? ExpectedEvidence,
    decimal? ExpectedValue,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<JobProfileCompetencyMatrixItemResponse>;

public sealed record JobProfileCompetencyMatrixItemPatchOperation(
    string Op,
    string Path,
    string? From,
    JsonElement? Value);

public sealed record PatchJobProfileCompetencyMatrixItemCommand(
    Guid JobProfileId,
    Guid ItemId,
    Guid ConcurrencyToken,
    IReadOnlyCollection<JobProfileCompetencyMatrixItemPatchOperation> Operations)
    : ICommand<JobProfileCompetencyMatrixItemResponse>;

public sealed record RemoveJobProfileCompetencyMatrixItemCommand(
    Guid JobProfileId,
    Guid ItemId,
    Guid ConcurrencyToken)
    : ICommand<JobProfileParentConcurrencyResult>;

internal sealed class GetJobProfileCompetencyMatrixQueryValidator : AbstractValidator<GetJobProfileCompetencyMatrixQuery>
{
    public GetJobProfileCompetencyMatrixQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class GetJobProfileCompetencyMatrixItemQueryValidator : AbstractValidator<GetJobProfileCompetencyMatrixItemQuery>
{
    public GetJobProfileCompetencyMatrixItemQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
        RuleFor(query => query.ItemId).NotEmpty();
    }
}

internal sealed class ExportJobProfileCompetencyMatrixQueryValidator : AbstractValidator<ExportJobProfileCompetencyMatrixQuery>
{
    public ExportJobProfileCompetencyMatrixQueryValidator()
    {
        RuleFor(query => query.JobProfileId).NotEmpty();
    }
}

internal sealed class AddJobProfileCompetencyMatrixItemCommandValidator : AbstractValidator<AddJobProfileCompetencyMatrixItemCommand>
{
    public AddJobProfileCompetencyMatrixItemCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.OccupationalPyramidLevelId).NotEmpty();
        RuleFor(command => command.ExpectedEvidence).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConductIds)
            .NotEmpty()
            .WithMessage("At least one competency conduct is required per matrix item.")
            .Must(static conductIds => conductIds is null || conductIds.Count <= CompetencyFrameworkValidationRules.MaxConductsPerMatrixItem)
            .WithMessage("A maximum of 50 conducts per competency matrix item is allowed.");
        RuleForEach(command => command.ConductIds).NotEqual(Guid.Empty);
    }
}

internal sealed class UpdateJobProfileCompetencyMatrixItemCommandValidator : AbstractValidator<UpdateJobProfileCompetencyMatrixItemCommand>
{
    public UpdateJobProfileCompetencyMatrixItemCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.OccupationalPyramidLevelId).NotEmpty();
        RuleFor(command => command.ExpectedEvidence).MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConductIds)
            .NotEmpty()
            .WithMessage("At least one competency conduct is required per matrix item.")
            .Must(static conductIds => conductIds is null || conductIds.Count <= CompetencyFrameworkValidationRules.MaxConductsPerMatrixItem)
            .WithMessage("A maximum of 50 conducts per competency matrix item is allowed.");
        RuleForEach(command => command.ConductIds).NotEqual(Guid.Empty);
    }
}

internal sealed class PatchJobProfileCompetencyMatrixItemCommandValidator : AbstractValidator<PatchJobProfileCompetencyMatrixItemCommand>
{
    public PatchJobProfileCompetencyMatrixItemCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Operations).NotEmpty();
        RuleFor(command => command.Operations)
            .Must(static operations => operations.Count <= JsonPatchHardening.MaxOperationsPerDocument)
            .WithMessage(JsonPatchHardening.MaxOperationsMessage);
        RuleForEach(command => command.Operations).ChildRules(operation =>
        {
            operation.RuleFor(item => item.Op).NotEmpty();
            operation.RuleFor(item => item.Path).NotEmpty();
        });
    }
}

internal sealed class RemoveJobProfileCompetencyMatrixItemCommandValidator : AbstractValidator<RemoveJobProfileCompetencyMatrixItemCommand>
{
    public RemoveJobProfileCompetencyMatrixItemCommandValidator()
    {
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
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

internal sealed class GetJobProfileCompetencyMatrixItemQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    ITenantContext tenantContext)
    : IQueryHandler<GetJobProfileCompetencyMatrixItemQuery, JobProfileCompetencyMatrixItemResponse>
{
    public async Task<Result<JobProfileCompetencyMatrixItemResponse>> Handle(
        GetJobProfileCompetencyMatrixItemQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(authorizationResult.Error);
        }

        var item = await repository.GetJobProfileCompetencyMatrixItemResponseAsync(query.JobProfileId, query.ItemId, cancellationToken);
        if (item is not null)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Success(item);
        }

        return Result<JobProfileCompetencyMatrixItemResponse>.Failure(
            await repository.JobProfileExistsOutsideTenantAsync(query.JobProfileId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CompetencyFrameworkErrors.JobProfileCompetencyMatrixItemNotFound);
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

internal sealed class AddJobProfileCompetencyMatrixItemCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddJobProfileCompetencyMatrixItemCommand, JobProfileCompetencyMatrixItemResponse>
{
    public async Task<Result<JobProfileCompetencyMatrixItemResponse>> Handle(
        AddJobProfileCompetencyMatrixItemCommand command,
        CancellationToken cancellationToken)
    {
        var context = await JobProfileCompetencyMatrixItemSupport.LoadProfileForManageAsync(
            command.JobProfileId,
            authorizationService,
            repository,
            tenantContext,
            cancellationToken);
        if (context.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(context.Error);
        }

        var profile = context.Value;

        var currentCount = await repository.CountExpectationsByJobProfileIdAsync(profile.Id, cancellationToken);
        if (currentCount >= CompetencyFrameworkValidationRules.MaxMatrixItems)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixItemLimitReached);
        }

        var resolution = await JobProfileCompetencyMatrixItemSupport.ResolveItemAsync(
            command.OccupationalPyramidLevelId,
            command.ConductIds,
            profile.TenantId,
            repository,
            authorizationService,
            cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        if (await repository.ExpectationTupleExistsAsync(
                profile.Id,
                resolved.Level.Id,
                resolved.Conducts.CompetencyCatalogItemId,
                resolved.Conducts.CompetencyTypeCatalogItemId,
                resolved.Conducts.BehaviorLevelCatalogItemId,
                excludingExpectationInternalId: null,
                cancellationToken))
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
        }

        var expectation = JobProfileCompetencyExpectation.Create(
            profile.Id,
            resolved.Level.Id,
            resolved.Conducts.CompetencyCatalogItemId,
            resolved.Conducts.CompetencyTypeCatalogItemId,
            resolved.Conducts.BehaviorLevelCatalogItemId,
            command.ExpectedEvidence,
            command.ExpectedValue,
            command.SortOrder);
        expectation.SetTenantId(profile.TenantId);
        expectation.ReplaceConducts(resolved.Conducts.Links);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddExpectations([expectation]);
            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetJobProfileCompetencyMatrixItemResponseAsync(profile.PublicId, expectation.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile competency matrix item could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileCompetencyMatrixUpdated,
                    AuditEntityTypes.JobProfileCompetencyMatrix,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Added competency matrix item to job profile {profile.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCompetencyMatrixItemResponse>.Success(response);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateJobProfileCompetencyMatrixItemCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateJobProfileCompetencyMatrixItemCommand, JobProfileCompetencyMatrixItemResponse>
{
    public async Task<Result<JobProfileCompetencyMatrixItemResponse>> Handle(
        UpdateJobProfileCompetencyMatrixItemCommand command,
        CancellationToken cancellationToken)
    {
        var context = await JobProfileCompetencyMatrixItemSupport.LoadProfileForManageAsync(
            command.JobProfileId,
            authorizationService,
            repository,
            tenantContext,
            cancellationToken);
        if (context.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(context.Error);
        }

        var profile = context.Value;

        var expectation = await repository.GetExpectationAggregateAsync(profile.Id, command.ItemId, cancellationToken);
        if (expectation is null)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixItemNotFound);
        }

        if (expectation.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var resolution = await JobProfileCompetencyMatrixItemSupport.ResolveItemAsync(
            command.OccupationalPyramidLevelId,
            command.ConductIds,
            profile.TenantId,
            repository,
            authorizationService,
            cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        if (await repository.ExpectationTupleExistsAsync(
                profile.Id,
                resolved.Level.Id,
                resolved.Conducts.CompetencyCatalogItemId,
                resolved.Conducts.CompetencyTypeCatalogItemId,
                resolved.Conducts.BehaviorLevelCatalogItemId,
                excludingExpectationInternalId: expectation.Id,
                cancellationToken))
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
        }

        var before = await repository.GetJobProfileCompetencyMatrixItemResponseAsync(profile.PublicId, expectation.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            expectation.ReplaceConducts(resolved.Conducts.Links);
            expectation.Update(
                resolved.Level.Id,
                resolved.Conducts.CompetencyCatalogItemId,
                resolved.Conducts.CompetencyTypeCatalogItemId,
                resolved.Conducts.BehaviorLevelCatalogItemId,
                command.ExpectedEvidence,
                command.ExpectedValue,
                command.SortOrder);
            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetJobProfileCompetencyMatrixItemResponseAsync(profile.PublicId, expectation.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile competency matrix item could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileCompetencyMatrixUpdated,
                    AuditEntityTypes.JobProfileCompetencyMatrix,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Updated competency matrix item in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCompetencyMatrixItemResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PatchJobProfileCompetencyMatrixItemCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PatchJobProfileCompetencyMatrixItemCommand, JobProfileCompetencyMatrixItemResponse>
{
    public async Task<Result<JobProfileCompetencyMatrixItemResponse>> Handle(
        PatchJobProfileCompetencyMatrixItemCommand command,
        CancellationToken cancellationToken)
    {
        var context = await JobProfileCompetencyMatrixItemSupport.LoadProfileForManageAsync(
            command.JobProfileId,
            authorizationService,
            repository,
            tenantContext,
            cancellationToken);
        if (context.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(context.Error);
        }

        var profile = context.Value;

        var expectation = await repository.GetExpectationAggregateAsync(profile.Id, command.ItemId, cancellationToken);
        if (expectation is null)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixItemNotFound);
        }

        if (expectation.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var before = await repository.GetJobProfileCompetencyMatrixItemResponseAsync(profile.PublicId, expectation.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Job profile competency matrix item could not be resolved before patch.");

        var patchState = JobProfileCompetencyMatrixItemPatchState.From(before);
        var patchApplication = JobProfileCompetencyMatrixItemPatchApplier.Apply(command.Operations, patchState);
        if (patchApplication.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(patchApplication.Error);
        }

        var validation = JobProfileCompetencyMatrixItemPatchApplier.Validate(patchState);
        if (validation.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(validation.Error);
        }

        if (!patchState.HasMutation)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Success(before);
        }

        var resolution = await JobProfileCompetencyMatrixItemSupport.ResolveItemAsync(
            patchState.OccupationalPyramidLevelId,
            patchState.ConductIds,
            profile.TenantId,
            repository,
            authorizationService,
            cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        if (await repository.ExpectationTupleExistsAsync(
                profile.Id,
                resolved.Level.Id,
                resolved.Conducts.CompetencyCatalogItemId,
                resolved.Conducts.CompetencyTypeCatalogItemId,
                resolved.Conducts.BehaviorLevelCatalogItemId,
                excludingExpectationInternalId: expectation.Id,
                cancellationToken))
        {
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            expectation.ReplaceConducts(resolved.Conducts.Links);
            expectation.Update(
                resolved.Level.Id,
                resolved.Conducts.CompetencyCatalogItemId,
                resolved.Conducts.CompetencyTypeCatalogItemId,
                resolved.Conducts.BehaviorLevelCatalogItemId,
                patchState.ExpectedEvidence,
                patchState.ExpectedValue,
                patchState.SortOrder);
            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetJobProfileCompetencyMatrixItemResponseAsync(profile.PublicId, expectation.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Job profile competency matrix item could not be resolved after patch.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileCompetencyMatrixUpdated,
                    AuditEntityTypes.JobProfileCompetencyMatrix,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Patched competency matrix item in job profile {profile.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileCompetencyMatrixItemResponse>.Success(after);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileCompetencyMatrixItemResponse>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RemoveJobProfileCompetencyMatrixItemCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveJobProfileCompetencyMatrixItemCommand, JobProfileParentConcurrencyResult>
{
    public async Task<Result<JobProfileParentConcurrencyResult>> Handle(
        RemoveJobProfileCompetencyMatrixItemCommand command,
        CancellationToken cancellationToken)
    {
        var context = await JobProfileCompetencyMatrixItemSupport.LoadProfileForManageAsync(
            command.JobProfileId,
            authorizationService,
            repository,
            tenantContext,
            cancellationToken);
        if (context.IsFailure)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(context.Error);
        }

        var profile = context.Value;

        var expectation = await repository.GetExpectationAggregateAsync(profile.Id, command.ItemId, cancellationToken);
        if (expectation is null)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixItemNotFound);
        }

        if (expectation.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<JobProfileParentConcurrencyResult>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var before = await repository.GetJobProfileCompetencyMatrixItemResponseAsync(profile.PublicId, expectation.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.RemoveExpectations([expectation]);
            profile.BumpVersion();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.JobProfileCompetencyMatrixUpdated,
                    AuditEntityTypes.JobProfileCompetencyMatrix,
                    profile.PublicId,
                    profile.Code,
                    AuditActions.Update,
                    $"Removed competency matrix item from job profile {profile.Code}.",
                    Before: before),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<JobProfileParentConcurrencyResult>.Success(new JobProfileParentConcurrencyResult(profile.ConcurrencyToken));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<JobProfileParentConcurrencyResult>.Failure(new Error("JobProfile.Conflict", ex.Message, ErrorType.Conflict));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed record DerivedMatrixConducts(
    long CompetencyCatalogItemId,
    long CompetencyTypeCatalogItemId,
    long BehaviorLevelCatalogItemId,
    IReadOnlyList<JobProfileCompetencyExpectationConduct> Links);

internal sealed record ResolvedMatrixItem(OccupationalPyramidLevel Level, DerivedMatrixConducts Conducts);

internal static class JobProfileCompetencyMatrixItemSupport
{
    public static async Task<Result<JobProfile>> LoadProfileForManageAsync(
        Guid jobProfileId,
        ICompetencyFrameworkAuthorizationService authorizationService,
        ICompetencyFrameworkRepository repository,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<JobProfile>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<JobProfile>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetJobProfileAggregateByIdAsync(jobProfileId, cancellationToken);
        if (profile is null)
        {
            return Result<JobProfile>.Failure(
                await repository.JobProfileExistsOutsideTenantAsync(jobProfileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.JobProfileNotFound);
        }

        if (profile.Status == JobProfileStatus.Archived)
        {
            return Result<JobProfile>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
        }

        return Result<JobProfile>.Success(profile);
    }

    public static async Task<Result<ResolvedMatrixItem>> ResolveItemAsync(
        Guid occupationalPyramidLevelId,
        IReadOnlyCollection<Guid> conductIds,
        Guid tenantId,
        ICompetencyFrameworkRepository repository,
        ICompetencyFrameworkAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        var levelsById = await repository.ResolveActiveOccupationalPyramidLevelsAsync(tenantId, [occupationalPyramidLevelId], cancellationToken);
        var levelResolution = await CompetencyFrameworkCatalogResolver.ResolvePyramidLevelFromMapAsync(
            levelsById,
            occupationalPyramidLevelId,
            repository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (levelResolution.IsFailure)
        {
            return Result<ResolvedMatrixItem>.Failure(levelResolution.Error);
        }

        var conductsResolution = await BuildConductsAsync(conductIds, tenantId, repository, authorizationService, cancellationToken);
        if (conductsResolution.IsFailure)
        {
            return Result<ResolvedMatrixItem>.Failure(conductsResolution.Error);
        }

        return Result<ResolvedMatrixItem>.Success(new ResolvedMatrixItem(levelResolution.Value, conductsResolution.Value));
    }

    // Derive the item's competency / type / behavior-level triple from its conducts. A matrix item is a
    // single cell of the matrix, so every conduct it lists must share one triple; a divergent conduct (or a
    // duplicate conduct) is a matrix-constraint conflict. The validator guarantees >= 1 conduct.
    private static async Task<Result<DerivedMatrixConducts>> BuildConductsAsync(
        IReadOnlyCollection<Guid> conductIds,
        Guid tenantId,
        ICompetencyFrameworkRepository repository,
        ICompetencyFrameworkAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        var conductsById = await repository.ResolveActiveCompetencyConductsAsync(tenantId, conductIds.ToArray(), cancellationToken);

        var links = new List<JobProfileCompetencyExpectationConduct>();
        var seen = new HashSet<Guid>();
        long competencyCatalogItemId = 0;
        long competencyTypeCatalogItemId = 0;
        long behaviorLevelCatalogItemId = 0;
        var sortOrder = 0;

        foreach (var conductId in conductIds)
        {
            if (!seen.Add(conductId))
            {
                return Result<DerivedMatrixConducts>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
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
                return Result<DerivedMatrixConducts>.Failure(conductResolution.Error);
            }

            var conduct = conductResolution.Value;
            if (seen.Count == 1)
            {
                competencyCatalogItemId = conduct.CompetencyCatalogItemId;
                competencyTypeCatalogItemId = conduct.CompetencyTypeCatalogItemId;
                behaviorLevelCatalogItemId = conduct.BehaviorLevelCatalogItemId;
            }
            else if (conduct.CompetencyCatalogItemId != competencyCatalogItemId ||
                conduct.CompetencyTypeCatalogItemId != competencyTypeCatalogItemId ||
                conduct.BehaviorLevelCatalogItemId != behaviorLevelCatalogItemId)
            {
                return Result<DerivedMatrixConducts>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
            }

            var link = JobProfileCompetencyExpectationConduct.Create(conduct.Id, sortOrder++);
            link.SetTenantId(tenantId);
            links.Add(link);
        }

        return Result<DerivedMatrixConducts>.Success(new DerivedMatrixConducts(
            competencyCatalogItemId,
            competencyTypeCatalogItemId,
            behaviorLevelCatalogItemId,
            links));
    }
}

internal sealed class JobProfileCompetencyMatrixItemPatchState
{
    public Guid OccupationalPyramidLevelId { get; set; }
    public List<Guid> ConductIds { get; set; } = [];
    public string? ExpectedEvidence { get; set; }
    public decimal? ExpectedValue { get; set; }
    public int SortOrder { get; set; }
    public bool HasMutation { get; set; }

    public static JobProfileCompetencyMatrixItemPatchState From(JobProfileCompetencyMatrixItemResponse response) =>
        new()
        {
            OccupationalPyramidLevelId = response.OccupationalPyramidLevelId,
            ConductIds = response.Conducts.Select(conduct => conduct.ConductId).ToList(),
            ExpectedEvidence = response.ExpectedEvidence,
            ExpectedValue = response.ExpectedValue,
            SortOrder = response.SortOrder
        };
}

internal static class JobProfileCompetencyMatrixItemPatchApplier
{
    private static readonly HashSet<string> SupportedOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "add",
        "replace",
        "remove"
    };

    public static Result Apply(IReadOnlyCollection<JobProfileCompetencyMatrixItemPatchOperation> operations, JobProfileCompetencyMatrixItemPatchState state)
    {
        foreach (var operation in operations)
        {
            var op = operation.Op.Trim();
            if (!SupportedOperations.Contains(op))
            {
                return ValidationFailure(operation.Path, $"Unsupported JSON Patch operation '{operation.Op}'.");
            }

            var segments = ParsePath(operation.Path);
            if (segments.Length != 1)
            {
                return ValidationFailure(operation.Path, "Only root competency matrix item properties can be patched.");
            }

            try
            {
                var result = ApplyOperation(op, segments[0], operation.Value, state, operation.Path);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            catch (MatrixItemPatchValueException exception)
            {
                return ValidationFailure(exception.Path, exception.Message);
            }
        }

        return Result.Success();
    }

    public static Result Validate(JobProfileCompetencyMatrixItemPatchState state)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (state.OccupationalPyramidLevelId == Guid.Empty)
        {
            errors["occupationalPyramidLevelPublicId"] = ["OccupationalPyramidLevelPublicId must be a valid UUID."];
        }

        if (state.ConductIds.Count == 0)
        {
            errors["conductPublicIds"] = ["At least one competency conduct is required per matrix item."];
        }
        else if (state.ConductIds.Count > CompetencyFrameworkValidationRules.MaxConductsPerMatrixItem)
        {
            errors["conductPublicIds"] = ["A maximum of 50 conducts per competency matrix item is allowed."];
        }
        else if (state.ConductIds.Any(id => id == Guid.Empty))
        {
            errors["conductPublicIds"] = ["Conduct ids must be valid UUIDs."];
        }

        if (!string.IsNullOrEmpty(state.ExpectedEvidence) && state.ExpectedEvidence.Length > 1000)
        {
            errors["expectedEvidence"] = ["ExpectedEvidence must be 1000 characters or fewer."];
        }

        if (state.SortOrder < 0)
        {
            errors["sortOrder"] = ["SortOrder must be greater than or equal to 0."];
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCatalog.Validation(errors));
    }

    private static Result ApplyOperation(
        string op,
        string property,
        JsonElement? value,
        JobProfileCompetencyMatrixItemPatchState state,
        string path)
    {
        var isRemove = string.Equals(op, "remove", StringComparison.OrdinalIgnoreCase);

        if (IsAnySegment(property, "occupationalPyramidLevelPublicId", "occupationalPyramidLevelId"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "OccupationalPyramidLevelPublicId cannot be removed.");
            }

            state.OccupationalPyramidLevelId = ReadRequiredGuid(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "conductPublicIds"))
        {
            state.ConductIds = isRemove ? [] : ReadGuidArray(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "expectedEvidence"))
        {
            state.ExpectedEvidence = isRemove ? null : ReadNullableString(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "expectedValue"))
        {
            state.ExpectedValue = isRemove ? null : ReadNullableDecimal(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        if (IsSegment(property, "sortOrder"))
        {
            if (isRemove)
            {
                return ValidationFailure(path, "SortOrder cannot be removed.");
            }

            state.SortOrder = ReadInt(value, path);
            state.HasMutation = true;
            return Result.Success();
        }

        return ValidationFailure(path, $"Unsupported patch path '{path}'.");
    }

    private static string[] ParsePath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapeJsonPointerSegment)
            .ToArray();

    private static string UnescapeJsonPointerSegment(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

    private static bool IsNull(JsonElement? value) =>
        !value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    private static bool IsSegment(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsAnySegment(string actual, params string[] expected) =>
        expected.Any(item => IsSegment(actual, item));

    private static string? ReadNullableString(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            return null;
        }

        return value!.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : throw new MatrixItemPatchValueException(path, "Value must be a string or null.");
    }

    private static Guid ReadRequiredGuid(JsonElement? value, string path)
    {
        var raw = ReadNullableString(value, path);
        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var parsed))
        {
            throw new MatrixItemPatchValueException(path, "Value must be a valid UUID.");
        }

        return parsed;
    }

    private static List<Guid> ReadGuidArray(JsonElement? value, string path)
    {
        if (IsNull(value) || value!.Value.ValueKind != JsonValueKind.Array)
        {
            throw new MatrixItemPatchValueException(path, "Value must be an array of UUIDs.");
        }

        var ids = new List<Guid>();
        foreach (var element in value.Value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String || !Guid.TryParse(element.GetString(), out var parsed))
            {
                throw new MatrixItemPatchValueException(path, "Value must be an array of UUIDs.");
            }

            ids.Add(parsed);
        }

        return ids;
    }

    private static int ReadInt(JsonElement? value, string path)
    {
        if (IsNull(value))
        {
            throw new MatrixItemPatchValueException(path, "Value must be an integer.");
        }

        if (value!.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        var raw = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out parsed))
        {
            return parsed;
        }

        throw new MatrixItemPatchValueException(path, "Value must be an integer.");
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
            decimal.TryParse(raw, global::System.Globalization.NumberStyles.Number, global::System.Globalization.CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        throw new MatrixItemPatchValueException(path, "Value must be a number or null.");
    }

    private static Result ValidationFailure(string path, string message) =>
        Result.Failure(ErrorCatalog.Validation(new Dictionary<string, string[]>
        {
            [path.TrimStart('/')] = [message]
        }));

    private sealed class MatrixItemPatchValueException(string path, string message) : Exception(message)
    {
        public string Path { get; } = path;
    }
}
