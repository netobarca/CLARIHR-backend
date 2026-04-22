using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;
using FluentValidation;

namespace CLARIHR.Application.Features.CompetencyFramework;

public sealed record OccupationalPyramidLevelListItemResponse(
    Guid Id,
    string Code,
    string Name,
    int LevelOrder,
    string? Description,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record OccupationalPyramidLevelResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    int LevelOrder,
    string? Description,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record CompetencyConductBehaviorResponse(
    Guid BehaviorId,
    string BehaviorCode,
    string BehaviorName,
    string? Notes,
    int SortOrder);

public sealed record CompetencyConductListItemResponse(
    Guid Id,
    Guid CompanyId,
    Guid CompetencyId,
    string CompetencyCode,
    string CompetencyName,
    Guid CompetencyTypeId,
    string CompetencyTypeCode,
    string CompetencyTypeName,
    Guid BehaviorLevelId,
    string BehaviorLevelCode,
    string BehaviorLevelName,
    string Description,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record CompetencyConductResponse(
    Guid Id,
    Guid CompanyId,
    Guid CompetencyId,
    string CompetencyCode,
    string CompetencyName,
    Guid CompetencyTypeId,
    string CompetencyTypeCode,
    string CompetencyTypeName,
    Guid BehaviorLevelId,
    string BehaviorLevelCode,
    string BehaviorLevelName,
    string Description,
    int SortOrder,
    bool IsActive,
    IReadOnlyCollection<CompetencyConductBehaviorResponse> Behaviors,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

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
    AllowedActionsResponse? AllowedActions = null);

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

public sealed record CompetencyConductBehaviorInput(
    Guid BehaviorId,
    string? Notes,
    int SortOrder);

public sealed record JobProfileCompetencyMatrixItemInput(
    Guid OccupationalPyramidLevelId,
    Guid CompetencyId,
    Guid CompetencyTypeId,
    Guid BehaviorLevelId,
    IReadOnlyCollection<Guid> ConductIds,
    string? ExpectedEvidence,
    int SortOrder);

public sealed record SearchOccupationalPyramidLevelsQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = CompetencyFrameworkValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<OccupationalPyramidLevelListItemResponse>>;

public sealed record GetOccupationalPyramidLevelByIdQuery(Guid LevelId)
    : IQuery<OccupationalPyramidLevelResponse>;

public sealed record CreateOccupationalPyramidLevelCommand(
    Guid CompanyId,
    string Code,
    string Name,
    int LevelOrder,
    string? Description)
    : ICommand<OccupationalPyramidLevelResponse>;

public sealed record UpdateOccupationalPyramidLevelCommand(
    Guid LevelId,
    string Code,
    string Name,
    int LevelOrder,
    string? Description,
    Guid ConcurrencyToken)
    : ICommand<OccupationalPyramidLevelResponse>;

public sealed record ActivateOccupationalPyramidLevelCommand(Guid LevelId, Guid ConcurrencyToken)
    : ICommand<OccupationalPyramidLevelResponse>;

public sealed record InactivateOccupationalPyramidLevelCommand(Guid LevelId, Guid ConcurrencyToken)
    : ICommand<OccupationalPyramidLevelResponse>;

public sealed record SearchCompetencyConductsQuery(
    Guid CompanyId,
    Guid? CompetencyId,
    Guid? CompetencyTypeId,
    Guid? BehaviorLevelId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = CompetencyFrameworkValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<CompetencyConductListItemResponse>>;

public sealed record GetCompetencyConductByIdQuery(Guid ConductId)
    : IQuery<CompetencyConductResponse>;

public sealed record CreateCompetencyConductCommand(
    Guid CompanyId,
    Guid CompetencyId,
    Guid CompetencyTypeId,
    Guid BehaviorLevelId,
    string Description,
    int SortOrder)
    : ICommand<CompetencyConductResponse>;

public sealed record UpdateCompetencyConductCommand(
    Guid ConductId,
    Guid CompetencyId,
    Guid CompetencyTypeId,
    Guid BehaviorLevelId,
    string Description,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<CompetencyConductResponse>;

public sealed record ActivateCompetencyConductCommand(Guid ConductId, Guid ConcurrencyToken)
    : ICommand<CompetencyConductResponse>;

public sealed record InactivateCompetencyConductCommand(Guid ConductId, Guid ConcurrencyToken)
    : ICommand<CompetencyConductResponse>;

public sealed record UpdateCompetencyConductBehaviorsCommand(
    Guid ConductId,
    IReadOnlyCollection<CompetencyConductBehaviorInput> Behaviors,
    Guid ConcurrencyToken)
    : ICommand<CompetencyConductResponse>;

public sealed record GetJobProfileCompetencyMatrixQuery(Guid JobProfileId)
    : IQuery<JobProfileCompetencyMatrixResponse>;

public sealed record ExportJobProfileCompetencyMatrixQuery(Guid JobProfileId, int? MaxRows = null)
    : IQuery<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>;

public sealed record UpdateJobProfileCompetencyMatrixCommand(
    Guid JobProfileId,
    IReadOnlyCollection<JobProfileCompetencyMatrixItemInput> Items,
    Guid ConcurrencyToken)
    : ICommand<JobProfileCompetencyMatrixResponse>;

internal sealed class SearchOccupationalPyramidLevelsQueryValidator : AbstractValidator<SearchOccupationalPyramidLevelsQuery>
{
    public SearchOccupationalPyramidLevelsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, CompetencyFrameworkValidationRules.MaxPageSize);
    }
}

internal sealed class GetOccupationalPyramidLevelByIdQueryValidator : AbstractValidator<GetOccupationalPyramidLevelByIdQuery>
{
    public GetOccupationalPyramidLevelByIdQueryValidator()
    {
        RuleFor(query => query.LevelId).NotEmpty();
    }
}

internal sealed class CreateOccupationalPyramidLevelCommandValidator : AbstractValidator<CreateOccupationalPyramidLevelCommand>
{
    public CreateOccupationalPyramidLevelCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(CompetencyFrameworkValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.LevelOrder).GreaterThan(0);
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

internal sealed class UpdateOccupationalPyramidLevelCommandValidator : AbstractValidator<UpdateOccupationalPyramidLevelCommand>
{
    public UpdateOccupationalPyramidLevelCommandValidator()
    {
        RuleFor(command => command.LevelId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(CompetencyFrameworkValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.LevelOrder).GreaterThan(0);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateOccupationalPyramidLevelCommandValidator : AbstractValidator<ActivateOccupationalPyramidLevelCommand>
{
    public ActivateOccupationalPyramidLevelCommandValidator()
    {
        RuleFor(command => command.LevelId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateOccupationalPyramidLevelCommandValidator : AbstractValidator<InactivateOccupationalPyramidLevelCommand>
{
    public InactivateOccupationalPyramidLevelCommandValidator()
    {
        RuleFor(command => command.LevelId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchCompetencyConductsQueryValidator : AbstractValidator<SearchCompetencyConductsQuery>
{
    public SearchCompetencyConductsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.CompetencyId).NotEqual(Guid.Empty).When(static query => query.CompetencyId.HasValue);
        RuleFor(query => query.CompetencyTypeId).NotEqual(Guid.Empty).When(static query => query.CompetencyTypeId.HasValue);
        RuleFor(query => query.BehaviorLevelId).NotEqual(Guid.Empty).When(static query => query.BehaviorLevelId.HasValue);
        RuleFor(query => query)
            .Must(static query =>
            {
                var selectedFilters = 0;
                selectedFilters += query.CompetencyId.HasValue ? 1 : 0;
                selectedFilters += query.CompetencyTypeId.HasValue ? 1 : 0;
                selectedFilters += query.BehaviorLevelId.HasValue ? 1 : 0;

                return selectedFilters is 0 or 3;
            })
            .WithMessage("CompetencyId, CompetencyTypeId and BehaviorLevelId must be provided together.");
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, CompetencyFrameworkValidationRules.MaxPageSize);
    }
}

internal sealed class GetCompetencyConductByIdQueryValidator : AbstractValidator<GetCompetencyConductByIdQuery>
{
    public GetCompetencyConductByIdQueryValidator()
    {
        RuleFor(query => query.ConductId).NotEmpty();
    }
}

internal sealed class CreateCompetencyConductCommandValidator : AbstractValidator<CreateCompetencyConductCommand>
{
    public CreateCompetencyConductCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.CompetencyId).NotEmpty();
        RuleFor(command => command.CompetencyTypeId).NotEmpty();
        RuleFor(command => command.BehaviorLevelId).NotEmpty();
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateCompetencyConductCommandValidator : AbstractValidator<UpdateCompetencyConductCommand>
{
    public UpdateCompetencyConductCommandValidator()
    {
        RuleFor(command => command.ConductId).NotEmpty();
        RuleFor(command => command.CompetencyId).NotEmpty();
        RuleFor(command => command.CompetencyTypeId).NotEmpty();
        RuleFor(command => command.BehaviorLevelId).NotEmpty();
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateCompetencyConductCommandValidator : AbstractValidator<ActivateCompetencyConductCommand>
{
    public ActivateCompetencyConductCommandValidator()
    {
        RuleFor(command => command.ConductId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateCompetencyConductCommandValidator : AbstractValidator<InactivateCompetencyConductCommand>
{
    public InactivateCompetencyConductCommandValidator()
    {
        RuleFor(command => command.ConductId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UpdateCompetencyConductBehaviorsCommandValidator : AbstractValidator<UpdateCompetencyConductBehaviorsCommand>
{
    public UpdateCompetencyConductBehaviorsCommandValidator()
    {
        RuleFor(command => command.ConductId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleForEach(command => command.Behaviors).SetValidator(new CompetencyConductBehaviorInputValidator());
    }
}

internal sealed class CompetencyConductBehaviorInputValidator : AbstractValidator<CompetencyConductBehaviorInput>
{
    public CompetencyConductBehaviorInputValidator()
    {
        RuleFor(input => input.BehaviorId).NotEmpty();
        RuleFor(input => input.Notes).MaximumLength(1000);
        RuleFor(input => input.SortOrder).GreaterThanOrEqualTo(0);
    }
}

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
        RuleForEach(command => command.Items).SetValidator(new JobProfileCompetencyMatrixItemInputValidator());
    }
}

internal sealed class JobProfileCompetencyMatrixItemInputValidator : AbstractValidator<JobProfileCompetencyMatrixItemInput>
{
    public JobProfileCompetencyMatrixItemInputValidator()
    {
        RuleFor(item => item.OccupationalPyramidLevelId).NotEmpty();
        RuleFor(item => item.CompetencyId).NotEmpty();
        RuleFor(item => item.CompetencyTypeId).NotEmpty();
        RuleFor(item => item.BehaviorLevelId).NotEmpty();
        RuleFor(item => item.ExpectedEvidence).MaximumLength(1000);
        RuleFor(item => item.SortOrder).GreaterThanOrEqualTo(0);
        RuleForEach(item => item.ConductIds).NotEqual(Guid.Empty);
    }
}

internal sealed class SearchOccupationalPyramidLevelsQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchOccupationalPyramidLevelsQuery, PagedResponse<OccupationalPyramidLevelListItemResponse>>
{
    public async Task<Result<PagedResponse<OccupationalPyramidLevelListItemResponse>>> Handle(
        SearchOccupationalPyramidLevelsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<OccupationalPyramidLevelListItemResponse>>.Failure(authorizationResult.Error);
        }

        var payload = await repository.SearchOccupationalPyramidLevelsAsync(
            query.CompanyId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<OccupationalPyramidLevelListItemResponse>>.Success(payload);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = payload.Items
            .Select(item => CompetencyFrameworkPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();

        return Result<PagedResponse<OccupationalPyramidLevelListItemResponse>>.Success(payload with { Items = items });
    }
}

internal sealed class GetOccupationalPyramidLevelByIdQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetOccupationalPyramidLevelByIdQuery, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        GetOccupationalPyramidLevelByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetOccupationalPyramidLevelResponseByIdAsync(query.LevelId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = CompetencyFrameworkPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
            return Result<OccupationalPyramidLevelResponse>.Success(response);
        }

        return Result<OccupationalPyramidLevelResponse>.Failure(
            await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(query.LevelId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
    }
}

internal sealed class CreateOccupationalPyramidLevelCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOccupationalPyramidLevelCommand, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        CreateOccupationalPyramidLevelCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.OccupationalPyramidLevelCodeExistsAsync(command.CompanyId, normalizedCode, excludingInternalId: null, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelCodeConflict);
        }

        if (await repository.OccupationalPyramidLevelOrderExistsAsync(command.CompanyId, command.LevelOrder, excludingInternalId: null, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelOrderConflict);
        }

        var level = OccupationalPyramidLevel.Create(command.Code, command.Name, command.LevelOrder, command.Description);
        level.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddOccupationalPyramidLevel(level);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OccupationalPyramidLevelCreated,
                    AuditEntityTypes.OccupationalPyramidLevel,
                    level.PublicId,
                    level.Code,
                    AuditActions.Create,
                    $"Created occupational pyramid level {level.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OccupationalPyramidLevelResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateOccupationalPyramidLevelCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateOccupationalPyramidLevelCommand, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        UpdateOccupationalPyramidLevelCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetOccupationalPyramidLevelByIdAsync(command.LevelId, cancellationToken);
        if (level is null)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(
                await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(command.LevelId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
        }

        if (level.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var normalizedCode = command.Code.Trim().ToUpperInvariant();
        if (await repository.OccupationalPyramidLevelCodeExistsAsync(level.TenantId, normalizedCode, level.Id, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelCodeConflict);
        }

        if (await repository.OccupationalPyramidLevelOrderExistsAsync(level.TenantId, command.LevelOrder, level.Id, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelOrderConflict);
        }

        var before = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            level.Update(command.Code, command.Name, command.LevelOrder, command.Description);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OccupationalPyramidLevelUpdated,
                    AuditEntityTypes.OccupationalPyramidLevel,
                    level.PublicId,
                    level.Code,
                    AuditActions.Update,
                    $"Updated occupational pyramid level {level.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OccupationalPyramidLevelResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateOccupationalPyramidLevelCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateOccupationalPyramidLevelCommand, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        ActivateOccupationalPyramidLevelCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetOccupationalPyramidLevelByIdAsync(command.LevelId, cancellationToken);
        if (level is null)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(
                await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(command.LevelId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
        }

        if (level.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var before = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            level.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OccupationalPyramidLevelActivated,
                    AuditEntityTypes.OccupationalPyramidLevel,
                    level.PublicId,
                    level.Code,
                    AuditActions.Reactivate,
                    $"Activated occupational pyramid level {level.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OccupationalPyramidLevelResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateOccupationalPyramidLevelCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateOccupationalPyramidLevelCommand, OccupationalPyramidLevelResponse>
{
    public async Task<Result<OccupationalPyramidLevelResponse>> Handle(
        InactivateOccupationalPyramidLevelCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(authorizationResult.Error);
        }

        var level = await repository.GetOccupationalPyramidLevelByIdAsync(command.LevelId, cancellationToken);
        if (level is null)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(
                await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(command.LevelId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
        }

        if (level.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        if (await repository.OccupationalPyramidLevelHasActiveUsageAsync(level.Id, cancellationToken))
        {
            return Result<OccupationalPyramidLevelResponse>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelInUse);
        }

        var before = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            level.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetOccupationalPyramidLevelResponseByIdAsync(level.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Occupational pyramid level response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OccupationalPyramidLevelInactivated,
                    AuditEntityTypes.OccupationalPyramidLevel,
                    level.PublicId,
                    level.Code,
                    AuditActions.Deactivate,
                    $"Inactivated occupational pyramid level {level.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<OccupationalPyramidLevelResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class SearchCompetencyConductsQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchCompetencyConductsQuery, PagedResponse<CompetencyConductListItemResponse>>
{
    public async Task<Result<PagedResponse<CompetencyConductListItemResponse>>> Handle(
        SearchCompetencyConductsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<CompetencyConductListItemResponse>>.Failure(authorizationResult.Error);
        }

        var payload = await repository.SearchCompetencyConductsAsync(
            query.CompanyId,
            query.CompetencyId,
            query.CompetencyTypeId,
            query.BehaviorLevelId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<CompetencyConductListItemResponse>>.Success(payload);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = payload.Items
            .Select(item => CompetencyFrameworkPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();

        return Result<PagedResponse<CompetencyConductListItemResponse>>.Success(payload with { Items = items });
    }
}

internal sealed class GetCompetencyConductByIdQueryHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetCompetencyConductByIdQuery, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        GetCompetencyConductByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetCompetencyConductResponseByIdAsync(query.ConductId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = CompetencyFrameworkPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
            return Result<CompetencyConductResponse>.Success(response);
        }

        return Result<CompetencyConductResponse>.Failure(
            await repository.CompetencyConductExistsOutsideTenantAsync(query.ConductId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : CompetencyFrameworkErrors.CompetencyConductNotFound);
    }
}

internal sealed class CreateCompetencyConductCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCompetencyConductCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        CreateCompetencyConductCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var competencyResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            command.CompanyId,
            command.CompetencyId,
            JobCatalogCategory.Competency,
            repository,
            authorizationService,
            RbacPermissionAction.Create,
            cancellationToken);
        if (competencyResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(competencyResolution.Error);
        }

        var typeResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            command.CompanyId,
            command.CompetencyTypeId,
            JobCatalogCategory.CompetencyType,
            repository,
            authorizationService,
            RbacPermissionAction.Create,
            cancellationToken);
        if (typeResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(typeResolution.Error);
        }

        var levelResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            command.CompanyId,
            command.BehaviorLevelId,
            JobCatalogCategory.BehaviorLevel,
            repository,
            authorizationService,
            RbacPermissionAction.Create,
            cancellationToken);
        if (levelResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(levelResolution.Error);
        }

        var normalizedDescription = command.Description.Trim().ToUpperInvariant();
        if (await repository.CompetencyConductDuplicateExistsAsync(
                command.CompanyId,
                competencyResolution.Value.Id,
                typeResolution.Value.Id,
                levelResolution.Value.Id,
                normalizedDescription,
                excludingInternalId: null,
                cancellationToken))
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.CompetencyConductDuplicate);
        }

        var conduct = CompetencyConduct.Create(
            competencyResolution.Value.Id,
            typeResolution.Value.Id,
            levelResolution.Value.Id,
            command.Description,
            command.SortOrder);
        conduct.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.AddCompetencyConduct(conduct);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyConductCreated,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Create,
                    $"Created competency conduct {conduct.Description}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCompetencyConductCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompetencyConductCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        UpdateCompetencyConductCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var conduct = await repository.GetCompetencyConductByIdAsync(command.ConductId, includeBehaviors: true, cancellationToken);
        if (conduct is null)
        {
            return Result<CompetencyConductResponse>.Failure(
                await repository.CompetencyConductExistsOutsideTenantAsync(command.ConductId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.CompetencyConductNotFound);
        }

        if (conduct.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var competencyResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            conduct.TenantId,
            command.CompetencyId,
            JobCatalogCategory.Competency,
            repository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (competencyResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(competencyResolution.Error);
        }

        var typeResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            conduct.TenantId,
            command.CompetencyTypeId,
            JobCatalogCategory.CompetencyType,
            repository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (typeResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(typeResolution.Error);
        }

        var levelResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
            conduct.TenantId,
            command.BehaviorLevelId,
            JobCatalogCategory.BehaviorLevel,
            repository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (levelResolution.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(levelResolution.Error);
        }

        var normalizedDescription = command.Description.Trim().ToUpperInvariant();
        if (await repository.CompetencyConductDuplicateExistsAsync(
                conduct.TenantId,
                competencyResolution.Value.Id,
                typeResolution.Value.Id,
                levelResolution.Value.Id,
                normalizedDescription,
                conduct.Id,
                cancellationToken))
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.CompetencyConductDuplicate);
        }

        var before = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Competency conduct response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            conduct.Update(
                competencyResolution.Value.Id,
                typeResolution.Value.Id,
                levelResolution.Value.Id,
                command.Description,
                command.SortOrder);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyConductUpdated,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Update,
                    $"Updated competency conduct {conduct.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateCompetencyConductCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateCompetencyConductCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        ActivateCompetencyConductCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var conduct = await repository.GetCompetencyConductByIdAsync(command.ConductId, includeBehaviors: true, cancellationToken);
        if (conduct is null)
        {
            return Result<CompetencyConductResponse>.Failure(
                await repository.CompetencyConductExistsOutsideTenantAsync(command.ConductId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.CompetencyConductNotFound);
        }

        if (conduct.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var before = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Competency conduct response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            conduct.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyConductActivated,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Reactivate,
                    $"Activated competency conduct {conduct.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateCompetencyConductCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateCompetencyConductCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        InactivateCompetencyConductCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var conduct = await repository.GetCompetencyConductByIdAsync(command.ConductId, includeBehaviors: true, cancellationToken);
        if (conduct is null)
        {
            return Result<CompetencyConductResponse>.Failure(
                await repository.CompetencyConductExistsOutsideTenantAsync(command.ConductId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.CompetencyConductNotFound);
        }

        if (conduct.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        if (await repository.CompetencyConductHasActiveUsageAsync(conduct.Id, cancellationToken))
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.CompetencyConductInUse);
        }

        var before = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Competency conduct response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            conduct.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyConductInactivated,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Deactivate,
                    $"Inactivated competency conduct {conduct.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCompetencyConductBehaviorsCommandHandler(
    ICompetencyFrameworkAuthorizationService authorizationService,
    ICompetencyFrameworkRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompetencyConductBehaviorsCommand, CompetencyConductResponse>
{
    public async Task<Result<CompetencyConductResponse>> Handle(
        UpdateCompetencyConductBehaviorsCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<CompetencyConductResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompetencyConductResponse>.Failure(authorizationResult.Error);
        }

        var conduct = await repository.GetCompetencyConductByIdAsync(command.ConductId, includeBehaviors: true, cancellationToken);
        if (conduct is null)
        {
            return Result<CompetencyConductResponse>.Failure(
                await repository.CompetencyConductExistsOutsideTenantAsync(command.ConductId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : CompetencyFrameworkErrors.CompetencyConductNotFound);
        }

        if (conduct.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.ConcurrencyConflict);
        }

        var behaviorById = new Dictionary<Guid, long>();
        foreach (var behavior in command.Behaviors)
        {
            if (behaviorById.ContainsKey(behavior.BehaviorId))
            {
                return Result<CompetencyConductResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
            }

            var resolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
                conduct.TenantId,
                behavior.BehaviorId,
                JobCatalogCategory.Behavior,
                repository,
                authorizationService,
                RbacPermissionAction.Update,
                cancellationToken);
            if (resolution.IsFailure)
            {
                return Result<CompetencyConductResponse>.Failure(resolution.Error);
            }

            behaviorById.Add(behavior.BehaviorId, resolution.Value.Id);
        }

        var before = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Competency conduct response could not be resolved before behavior update.");

        var behaviorEntities = command.Behaviors
            .Select(input =>
            {
                var entity = CompetencyConductBehavior.Create(
                    behaviorById[input.BehaviorId],
                    input.Notes,
                    input.SortOrder);
                entity.SetTenantId(conduct.TenantId);
                return entity;
            })
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            conduct.ReplaceBehaviors(behaviorEntities);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetCompetencyConductResponseByIdAsync(conduct.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Competency conduct response could not be resolved after behavior update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompetencyBehaviorLinked,
                    AuditEntityTypes.CompetencyConduct,
                    conduct.PublicId,
                    conduct.Description,
                    AuditActions.Update,
                    $"Updated behavior associations for competency conduct {conduct.Description}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyConductResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
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

        var matrixItems = new List<JobProfileCompetencyExpectation>();
        var uniqueCombinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in command.Items)
        {
            var levelResolution = await CompetencyFrameworkCatalogResolver.ResolvePyramidLevelAsync(
                profile.TenantId,
                item.OccupationalPyramidLevelId,
                repository,
                authorizationService,
                RbacPermissionAction.Update,
                cancellationToken);
            if (levelResolution.IsFailure)
            {
                return Result<JobProfileCompetencyMatrixResponse>.Failure(levelResolution.Error);
            }

            var competencyResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
                profile.TenantId,
                item.CompetencyId,
                JobCatalogCategory.Competency,
                repository,
                authorizationService,
                RbacPermissionAction.Update,
                cancellationToken);
            if (competencyResolution.IsFailure)
            {
                return Result<JobProfileCompetencyMatrixResponse>.Failure(competencyResolution.Error);
            }

            var typeResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
                profile.TenantId,
                item.CompetencyTypeId,
                JobCatalogCategory.CompetencyType,
                repository,
                authorizationService,
                RbacPermissionAction.Update,
                cancellationToken);
            if (typeResolution.IsFailure)
            {
                return Result<JobProfileCompetencyMatrixResponse>.Failure(typeResolution.Error);
            }

            var behaviorLevelResolution = await CompetencyFrameworkCatalogResolver.ResolveCatalogAsync(
                profile.TenantId,
                item.BehaviorLevelId,
                JobCatalogCategory.BehaviorLevel,
                repository,
                authorizationService,
                RbacPermissionAction.Update,
                cancellationToken);
            if (behaviorLevelResolution.IsFailure)
            {
                return Result<JobProfileCompetencyMatrixResponse>.Failure(behaviorLevelResolution.Error);
            }

            var uniqueKey = $"{levelResolution.Value.Id}:{competencyResolution.Value.Id}:{typeResolution.Value.Id}:{behaviorLevelResolution.Value.Id}";
            if (!uniqueCombinations.Add(uniqueKey))
            {
                return Result<JobProfileCompetencyMatrixResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
            }

            var expectation = JobProfileCompetencyExpectation.Create(
                profile.Id,
                levelResolution.Value.Id,
                competencyResolution.Value.Id,
                typeResolution.Value.Id,
                behaviorLevelResolution.Value.Id,
                item.ExpectedEvidence,
                item.SortOrder);
            expectation.SetTenantId(profile.TenantId);

            var conducts = new List<JobProfileCompetencyExpectationConduct>();
            var conductSet = new HashSet<Guid>();
            var conductSort = 0;
            foreach (var conductId in item.ConductIds)
            {
                if (!conductSet.Add(conductId))
                {
                    return Result<JobProfileCompetencyMatrixResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
                }

                var conductResolution = await CompetencyFrameworkCatalogResolver.ResolveConductAsync(
                    profile.TenantId,
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
                if (conduct.CompetencyCatalogItemId != competencyResolution.Value.Id ||
                    conduct.CompetencyTypeCatalogItemId != typeResolution.Value.Id ||
                    conduct.BehaviorLevelCatalogItemId != behaviorLevelResolution.Value.Id)
                {
                    return Result<JobProfileCompetencyMatrixResponse>.Failure(CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict);
                }

                var link = JobProfileCompetencyExpectationConduct.Create(conduct.Id, conductSort++);
                link.SetTenantId(profile.TenantId);
                conducts.Add(link);
            }

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
                profile.BenefitsSummary,
                profile.WorkingConditionSummary,
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

internal static class CompetencyFrameworkPolicyAdapter
{
    public static OccupationalPyramidLevelListItemResponse ApplyAllowedActions(
        OccupationalPyramidLevelListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var state = response.IsActive ? "Active" : "Inactive";
        var allowedActions = resourceActionPolicyService.Evaluate(new ResourceActionContext(
            CompetencyFrameworkPermissionCodes.ResourceKey,
            state,
            response.IsActive,
            SupportsEdit: true,
            EditAllowed: canManage,
            SupportsDelete: false,
            SupportsArchive: false,
            SupportsActivate: true,
            ActivateAllowed: canManage,
            SupportsInactivate: true,
            InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }

    public static OccupationalPyramidLevelResponse ApplyAllowedActions(
        OccupationalPyramidLevelResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var state = response.IsActive ? "Active" : "Inactive";
        var allowedActions = resourceActionPolicyService.Evaluate(new ResourceActionContext(
            CompetencyFrameworkPermissionCodes.ResourceKey,
            state,
            response.IsActive,
            SupportsEdit: true,
            EditAllowed: canManage,
            SupportsDelete: false,
            SupportsArchive: false,
            SupportsActivate: true,
            ActivateAllowed: canManage,
            SupportsInactivate: true,
            InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }

    public static CompetencyConductListItemResponse ApplyAllowedActions(
        CompetencyConductListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var state = response.IsActive ? "Active" : "Inactive";
        var allowedActions = resourceActionPolicyService.Evaluate(new ResourceActionContext(
            CompetencyFrameworkPermissionCodes.ResourceKey,
            state,
            response.IsActive,
            SupportsEdit: true,
            EditAllowed: canManage,
            SupportsDelete: false,
            SupportsArchive: false,
            SupportsActivate: true,
            ActivateAllowed: canManage,
            SupportsInactivate: true,
            InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }

    public static CompetencyConductResponse ApplyAllowedActions(
        CompetencyConductResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var state = response.IsActive ? "Active" : "Inactive";
        var allowedActions = resourceActionPolicyService.Evaluate(new ResourceActionContext(
            CompetencyFrameworkPermissionCodes.ResourceKey,
            state,
            response.IsActive,
            SupportsEdit: true,
            EditAllowed: canManage,
            SupportsDelete: false,
            SupportsArchive: false,
            SupportsActivate: true,
            ActivateAllowed: canManage,
            SupportsInactivate: true,
            InactivateAllowed: canManage));

        return response with { AllowedActions = allowedActions };
    }

    public static JobProfileCompetencyMatrixResponse ApplyAllowedActions(
        JobProfileCompetencyMatrixResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(new ResourceActionContext(
            CompetencyFrameworkPermissionCodes.ResourceKey,
            response.JobProfileStatus.ToString(),
            IsActive: response.JobProfileStatus != JobProfileStatus.Archived,
            SupportsEdit: true,
            EditAllowed: canManage,
            SupportsDelete: false,
            SupportsArchive: false,
            SupportsActivate: false,
            SupportsInactivate: false,
            NonEditableStates: [JobProfileStatus.Archived.ToString()]));

        return response with { AllowedActions = allowedActions };
    }
}

internal static class CompetencyFrameworkCatalogResolver
{
    public static async Task<Result<JobCatalogItem>> ResolveCatalogAsync(
        Guid tenantId,
        Guid catalogItemId,
        JobCatalogCategory category,
        ICompetencyFrameworkRepository repository,
        ICompetencyFrameworkAuthorizationService authorizationService,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        var catalogItem = await repository.ResolveActiveCatalogItemAsync(tenantId, category, catalogItemId, cancellationToken);
        if (catalogItem is not null)
        {
            return Result<JobCatalogItem>.Success(catalogItem);
        }

        if (await repository.CatalogItemExistsOutsideTenantAsync(catalogItemId, cancellationToken))
        {
            return Result<JobCatalogItem>.Failure(authorizationService.TenantMismatch(action));
        }

        return Result<JobCatalogItem>.Failure(category switch
        {
            JobCatalogCategory.Competency => CompetencyFrameworkErrors.CompetencyNotFound,
            JobCatalogCategory.CompetencyType => CompetencyFrameworkErrors.CompetencyTypeNotFound,
            JobCatalogCategory.BehaviorLevel => CompetencyFrameworkErrors.BehaviorLevelNotFound,
            JobCatalogCategory.Behavior => CompetencyFrameworkErrors.BehaviorNotFound,
            _ => CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict
        });
    }

    public static async Task<Result<OccupationalPyramidLevel>> ResolvePyramidLevelAsync(
        Guid tenantId,
        Guid levelId,
        ICompetencyFrameworkRepository repository,
        ICompetencyFrameworkAuthorizationService authorizationService,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        var level = await repository.ResolveActiveOccupationalPyramidLevelAsync(tenantId, levelId, cancellationToken);
        if (level is not null)
        {
            return Result<OccupationalPyramidLevel>.Success(level);
        }

        if (await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(levelId, cancellationToken))
        {
            return Result<OccupationalPyramidLevel>.Failure(authorizationService.TenantMismatch(action));
        }

        return Result<OccupationalPyramidLevel>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
    }

    public static async Task<Result<CompetencyConduct>> ResolveConductAsync(
        Guid tenantId,
        Guid conductId,
        ICompetencyFrameworkRepository repository,
        ICompetencyFrameworkAuthorizationService authorizationService,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        var conduct = await repository.ResolveActiveCompetencyConductAsync(tenantId, conductId, cancellationToken);
        if (conduct is not null)
        {
            return Result<CompetencyConduct>.Success(conduct);
        }

        if (await repository.CompetencyConductExistsOutsideTenantAsync(conductId, cancellationToken))
        {
            return Result<CompetencyConduct>.Failure(authorizationService.TenantMismatch(action));
        }

        return Result<CompetencyConduct>.Failure(CompetencyFrameworkErrors.CompetencyConductNotFound);
    }
}
