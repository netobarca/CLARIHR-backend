using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Domain.PositionSlots;
using FluentValidation;

namespace CLARIHR.Application.Features.PositionSlots;

public sealed record PositionSlotListItemResponse(
    Guid Id,
    string Code,
    string? Title,
    PositionSlotStatus Status,
    Guid JobProfileId,
    string JobProfileCode,
    string JobProfileTitle,
    Guid? RoleId,
    string? RoleName,
    Guid OrgUnitId,
    string OrgUnitName,
    Guid? WorkCenterId,
    string? WorkCenterName,
    Guid? ContractTypeId,
    string? ContractTypeCode,
    string? ContractTypeName,
    int MaxEmployees,
    int OccupiedEmployees,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record PositionSlotResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string? Title,
    PositionSlotStatus Status,
    Guid JobProfileId,
    string JobProfileCode,
    string JobProfileTitle,
    Guid? RoleId,
    string? RoleName,
    Guid OrgUnitId,
    string OrgUnitName,
    Guid? WorkCenterId,
    string? WorkCenterName,
    string? CostCenterCode,
    Guid? DirectDependencyPositionSlotId,
    string? DirectDependencyPositionSlotCode,
    Guid? FunctionalDependencyPositionSlotId,
    string? FunctionalDependencyPositionSlotCode,
    Guid? PositionCategoryId,
    Guid? PositionCategoryClassificationId,
    Guid? ContractTypeId,
    string? ContractTypeCode,
    string? ContractTypeName,
    int MaxEmployees,
    int OccupiedEmployees,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null);

public sealed record PositionSlotGraphNodeResponse(
    Guid Id,
    string Code,
    string Label,
    PositionSlotStatus Status,
    Guid JobProfileId,
    Guid OrgUnitId,
    Guid? WorkCenterId,
    Guid? ContractTypeId,
    string? ContractTypeCode,
    bool IsActive);

public sealed record PositionSlotGraphEdgeResponse(
    Guid FromId,
    Guid ToId,
    PositionSlotDependencyRelationType RelationType);

public sealed record PositionSlotGraphResponse(
    IReadOnlyCollection<PositionSlotGraphNodeResponse> Nodes,
    IReadOnlyCollection<PositionSlotGraphEdgeResponse> Edges);

public sealed record PositionSlotGraphNodeData(
    long InternalId,
    Guid Id,
    string Code,
    string Label,
    PositionSlotStatus Status,
    Guid JobProfileId,
    Guid OrgUnitId,
    Guid? WorkCenterId,
    long? DirectDependencyInternalId,
    Guid? DirectDependencyId,
    long? FunctionalDependencyInternalId,
    Guid? FunctionalDependencyId,
    Guid? ContractTypeId,
    string? ContractTypeCode,
    bool IsActive);

public sealed record PositionSlotExportRow(
    Guid Id,
    string Code,
    string? Title,
    PositionSlotStatus Status,
    string JobProfileCode,
    string JobProfileTitle,
    Guid? RoleId,
    string? RoleName,
    string OrgUnitCode,
    string OrgUnitName,
    string? WorkCenterCode,
    string? WorkCenterName,
    string? CostCenterCode,
    string? DirectDependencyCode,
    string? FunctionalDependencyCode,
    Guid? ContractTypeId,
    string? ContractTypeCode,
    string? ContractTypeName,
    int MaxEmployees,
    int OccupiedEmployees,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PositionSlotJobProfileLookup(
    long InternalJobProfileId,
    Guid JobProfileId,
    Guid? OrgUnitId,
    string? OrgUnitName,
    string? CostCenterCode,
    Guid? PositionCategoryId,
    Guid? PositionCategoryClassificationId,
    Guid? ContractTypeId,
    string? ContractTypeCode,
    string? ContractTypeName);

public sealed record SearchPositionSlotsQuery(
    Guid CompanyId,
    PositionSlotStatus? Status,
    Guid? JobProfileId,
    Guid? OrgUnitId,
    Guid? WorkCenterId,
    Guid? ContractTypeId,
    string? Search,
    int PageNumber = 1,
    int PageSize = PositionSlotValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<PositionSlotListItemResponse>>;

public sealed record GetPositionSlotByIdQuery(Guid PositionSlotId) : IQuery<PositionSlotResponse>;

public sealed record GetPositionSlotGraphQuery(
    Guid CompanyId,
    Guid? RootId,
    int? Depth,
    bool IncludeFunctional)
    : IQuery<PositionSlotGraphResponse>;

public sealed record GetPositionSlotExportRowsQuery(
    Guid CompanyId,
    PositionSlotStatus? Status,
    Guid? JobProfileId,
    Guid? OrgUnitId,
    Guid? WorkCenterId,
    Guid? ContractTypeId,
    string? Search)
    : IQuery<IReadOnlyCollection<PositionSlotExportRow>>;

public sealed record CreatePositionSlotCommand(
    Guid CompanyId,
    string Code,
    string? Title,
    Guid JobProfileId,
    Guid? RoleId,
    Guid? WorkCenterId,
    Guid? DirectDependencyPositionSlotId,
    Guid? FunctionalDependencyPositionSlotId,
    PositionSlotStatus Status,
    int MaxEmployees,
    int OccupiedEmployees,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes)
    : ICommand<PositionSlotResponse>;

public sealed record UpdatePositionSlotCommand(
    Guid PositionSlotId,
    string Code,
    string? Title,
    Guid JobProfileId,
    Guid? RoleId,
    Guid? WorkCenterId,
    int MaxEmployees,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    string? Notes,
    Guid ConcurrencyToken)
    : ICommand<PositionSlotResponse>;

public sealed record UpdatePositionSlotStatusCommand(
    Guid PositionSlotId,
    PositionSlotStatus Status,
    Guid ConcurrencyToken)
    : ICommand<PositionSlotResponse>;

public sealed record UpdatePositionSlotDependenciesCommand(
    Guid PositionSlotId,
    Guid? DirectDependencyPositionSlotId,
    Guid? FunctionalDependencyPositionSlotId,
    Guid ConcurrencyToken)
    : ICommand<PositionSlotResponse>;

public sealed record UpdatePositionSlotOccupancyCommand(
    Guid PositionSlotId,
    int OccupiedEmployees,
    Guid ConcurrencyToken)
    : ICommand<PositionSlotResponse>;

internal sealed class SearchPositionSlotsQueryValidator : AbstractValidator<SearchPositionSlotsQuery>
{
    public SearchPositionSlotsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.JobProfileId)
            .NotEqual(Guid.Empty)
            .When(static query => query.JobProfileId.HasValue);
        RuleFor(query => query.OrgUnitId)
            .NotEqual(Guid.Empty)
            .When(static query => query.OrgUnitId.HasValue);
        RuleFor(query => query.WorkCenterId)
            .NotEqual(Guid.Empty)
            .When(static query => query.WorkCenterId.HasValue);
        RuleFor(query => query.ContractTypeId)
            .NotEqual(Guid.Empty)
            .When(static query => query.ContractTypeId.HasValue);
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PositionSlotValidationRules.MaxPageSize);
    }
}

internal sealed class GetPositionSlotByIdQueryValidator : AbstractValidator<GetPositionSlotByIdQuery>
{
    public GetPositionSlotByIdQueryValidator()
    {
        RuleFor(query => query.PositionSlotId).NotEmpty();
    }
}

internal sealed class GetPositionSlotGraphQueryValidator : AbstractValidator<GetPositionSlotGraphQuery>
{
    public GetPositionSlotGraphQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.RootId)
            .NotEqual(Guid.Empty)
            .When(static query => query.RootId.HasValue);
        RuleFor(query => query.Depth)
            .InclusiveBetween(1, PositionSlotValidationRules.MaxGraphDepth)
            .When(static query => query.Depth.HasValue);
    }
}

internal sealed class GetPositionSlotExportRowsQueryValidator : AbstractValidator<GetPositionSlotExportRowsQuery>
{
    public GetPositionSlotExportRowsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.JobProfileId)
            .NotEqual(Guid.Empty)
            .When(static query => query.JobProfileId.HasValue);
        RuleFor(query => query.OrgUnitId)
            .NotEqual(Guid.Empty)
            .When(static query => query.OrgUnitId.HasValue);
        RuleFor(query => query.WorkCenterId)
            .NotEqual(Guid.Empty)
            .When(static query => query.WorkCenterId.HasValue);
        RuleFor(query => query.ContractTypeId)
            .NotEqual(Guid.Empty)
            .When(static query => query.ContractTypeId.HasValue);
        RuleFor(query => query.Search).MaximumLength(150);
    }
}

internal sealed class CreatePositionSlotCommandValidator : AbstractValidator<CreatePositionSlotCommand>
{
    public CreatePositionSlotCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(PositionSlotValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Title).MaximumLength(180);
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RoleId)
            .NotEqual(Guid.Empty)
            .When(static command => command.RoleId.HasValue);
        RuleFor(command => command.WorkCenterId)
            .NotEqual(Guid.Empty)
            .When(static command => command.WorkCenterId.HasValue);
        RuleFor(command => command.DirectDependencyPositionSlotId)
            .NotEqual(Guid.Empty)
            .When(static command => command.DirectDependencyPositionSlotId.HasValue);
        RuleFor(command => command.FunctionalDependencyPositionSlotId)
            .NotEqual(Guid.Empty)
            .When(static command => command.FunctionalDependencyPositionSlotId.HasValue);
        RuleFor(command => command.MaxEmployees).GreaterThanOrEqualTo(1);
        RuleFor(command => command.OccupiedEmployees)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(command => command.MaxEmployees)
            .WithMessage("OccupiedEmployees must be less than or equal to MaxEmployees.");
        RuleFor(command => command.EffectiveFromUtc).NotEqual(default(DateTime));
        RuleFor(command => command.EffectiveToUtc)
            .Must((command, effectiveToUtc) => !effectiveToUtc.HasValue || effectiveToUtc.Value >= command.EffectiveFromUtc)
            .WithMessage("EffectiveToUtc must be greater than or equal to EffectiveFromUtc.");
        RuleFor(command => command.Notes).MaximumLength(2000);
    }
}

internal sealed class UpdatePositionSlotCommandValidator : AbstractValidator<UpdatePositionSlotCommand>
{
    public UpdatePositionSlotCommandValidator()
    {
        RuleFor(command => command.PositionSlotId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Must(PositionSlotValidationRules.IsValidCode)
            .WithMessage("Code format is invalid.");
        RuleFor(command => command.Title).MaximumLength(180);
        RuleFor(command => command.JobProfileId).NotEmpty();
        RuleFor(command => command.RoleId)
            .NotEqual(Guid.Empty)
            .When(static command => command.RoleId.HasValue);
        RuleFor(command => command.WorkCenterId)
            .NotEqual(Guid.Empty)
            .When(static command => command.WorkCenterId.HasValue);
        RuleFor(command => command.MaxEmployees).GreaterThanOrEqualTo(1);
        RuleFor(command => command.EffectiveFromUtc).NotEqual(default(DateTime));
        RuleFor(command => command.EffectiveToUtc)
            .Must((command, effectiveToUtc) => !effectiveToUtc.HasValue || effectiveToUtc.Value >= command.EffectiveFromUtc)
            .WithMessage("EffectiveToUtc must be greater than or equal to EffectiveFromUtc.");
        RuleFor(command => command.Notes).MaximumLength(2000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UpdatePositionSlotStatusCommandValidator : AbstractValidator<UpdatePositionSlotStatusCommand>
{
    public UpdatePositionSlotStatusCommandValidator()
    {
        RuleFor(command => command.PositionSlotId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UpdatePositionSlotDependenciesCommandValidator : AbstractValidator<UpdatePositionSlotDependenciesCommand>
{
    public UpdatePositionSlotDependenciesCommandValidator()
    {
        RuleFor(command => command.PositionSlotId).NotEmpty();
        RuleFor(command => command.DirectDependencyPositionSlotId)
            .NotEqual(Guid.Empty)
            .When(static command => command.DirectDependencyPositionSlotId.HasValue);
        RuleFor(command => command.FunctionalDependencyPositionSlotId)
            .NotEqual(Guid.Empty)
            .When(static command => command.FunctionalDependencyPositionSlotId.HasValue);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class UpdatePositionSlotOccupancyCommandValidator : AbstractValidator<UpdatePositionSlotOccupancyCommand>
{
    public UpdatePositionSlotOccupancyCommandValidator()
    {
        RuleFor(command => command.PositionSlotId).NotEmpty();
        RuleFor(command => command.OccupiedEmployees).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class SearchPositionSlotsQueryHandler(
    IPositionSlotAuthorizationService authorizationService,
    IPositionSlotRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchPositionSlotsQuery, PagedResponse<PositionSlotListItemResponse>>
{
    public async Task<Result<PagedResponse<PositionSlotListItemResponse>>> Handle(
        SearchPositionSlotsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PositionSlotListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.Status,
            query.JobProfileId,
            query.OrgUnitId,
            query.WorkCenterId,
            query.ContractTypeId,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<PositionSlotListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => PositionSlotPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<PositionSlotListItemResponse>>.Success(response);
    }
}

internal sealed class GetPositionSlotByIdQueryHandler(
    IPositionSlotAuthorizationService authorizationService,
    IPositionSlotRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetPositionSlotByIdQuery, PositionSlotResponse>
{
    public async Task<Result<PositionSlotResponse>> Handle(
        GetPositionSlotByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionSlotResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.PositionSlotId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = PositionSlotPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);
            return Result<PositionSlotResponse>.Success(response);
        }

        return Result<PositionSlotResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.PositionSlotId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PositionSlotErrors.PositionSlotNotFound);
    }
}

internal sealed class GetPositionSlotGraphQueryHandler(
    IPositionSlotAuthorizationService authorizationService,
    IPositionSlotRepository repository)
    : IQueryHandler<GetPositionSlotGraphQuery, PositionSlotGraphResponse>
{
    public async Task<Result<PositionSlotGraphResponse>> Handle(
        GetPositionSlotGraphQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PositionSlotGraphResponse>.Failure(authorizationResult.Error);
        }

        var nodes = await repository.GetGraphNodesAsync(query.CompanyId, cancellationToken);

        if (query.RootId.HasValue && nodes.All(node => node.Id != query.RootId.Value))
        {
            return Result<PositionSlotGraphResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(query.RootId.Value, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                    : PositionSlotErrors.PositionSlotNotFound);
        }

        var graph = PositionSlotGraphBuilder.Build(nodes, query.RootId, query.Depth, query.IncludeFunctional);
        return Result<PositionSlotGraphResponse>.Success(graph);
    }
}

internal sealed class GetPositionSlotExportRowsQueryHandler(
    IPositionSlotAuthorizationService authorizationService,
    IPositionSlotRepository repository)
    : IQueryHandler<GetPositionSlotExportRowsQuery, IReadOnlyCollection<PositionSlotExportRow>>
{
    public async Task<Result<IReadOnlyCollection<PositionSlotExportRow>>> Handle(
        GetPositionSlotExportRowsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<PositionSlotExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await repository.GetExportRowsAsync(
            query.CompanyId,
            query.Status,
            query.JobProfileId,
            query.OrgUnitId,
            query.WorkCenterId,
            query.ContractTypeId,
            query.Search,
            cancellationToken);

        return Result<IReadOnlyCollection<PositionSlotExportRow>>.Success(rows);
    }
}

internal sealed class CreatePositionSlotCommandHandler(
    IPositionSlotAuthorizationService authorizationService,
    IPositionSlotRepository repository,
    IIamAdministrationRepository iamRepository,
    ICostCenterRepository costCenterRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePositionSlotCommand, PositionSlotResponse>
{
    public async Task<Result<PositionSlotResponse>> Handle(CreatePositionSlotCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(command.CompanyId, command.Code.Trim().ToUpperInvariant(), excludingSlotId: null, cancellationToken))
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.CodeConflict);
        }

        var jobProfileLookupResult = await PositionSlotCommandSupport.ResolveJobProfileLookupAsync(
            command.CompanyId,
            command.JobProfileId,
            authorizationService,
            repository,
            RbacPermissionAction.Create,
            cancellationToken);
        if (jobProfileLookupResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(jobProfileLookupResult.Error);
        }
        var jobProfileLookup = jobProfileLookupResult.Value;

        var workCenterIdResult = await PositionSlotCommandSupport.ResolveWorkCenterInternalIdAsync(
            command.CompanyId,
            command.WorkCenterId,
            authorizationService,
            repository,
            RbacPermissionAction.Create,
            cancellationToken);
        if (workCenterIdResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(workCenterIdResult.Error);
        }

        var roleIdResult = await PositionSlotCommandSupport.ResolveRoleInternalIdAsync(
            command.RoleId,
            iamRepository,
            cancellationToken);
        if (roleIdResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(roleIdResult.Error);
        }

        if (!jobProfileLookup.OrgUnitId.HasValue)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.JobProfileOrgUnitNotConfigured);
        }

        var isFixedTerm = jobProfileLookup.ContractTypeId.HasValue &&
            PositionSlotContractTypeRules.IsFixedTerm(
            jobProfileLookup.ContractTypeCode,
            jobProfileLookup.ContractTypeName);

        var directDependencyResult = await PositionSlotCommandSupport.ResolveDependencyInternalIdAsync(
            command.CompanyId,
            command.DirectDependencyPositionSlotId,
            authorizationService,
            repository,
            RbacPermissionAction.Create,
            cancellationToken);
        if (directDependencyResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(directDependencyResult.Error);
        }

        var functionalDependencyResult = await PositionSlotCommandSupport.ResolveDependencyInternalIdAsync(
            command.CompanyId,
            command.FunctionalDependencyPositionSlotId,
            authorizationService,
            repository,
            RbacPermissionAction.Create,
            cancellationToken);
        if (functionalDependencyResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(functionalDependencyResult.Error);
        }

        if (!string.IsNullOrWhiteSpace(jobProfileLookup.CostCenterCode) &&
            !await costCenterRepository.ExistsActiveByCodeAsync(
                command.CompanyId,
                jobProfileLookup.CostCenterCode.Trim().ToUpperInvariant(),
                cancellationToken))
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.CostCenterInvalid);
        }

        PositionSlot slot;
        try
        {
            slot = PositionSlot.Create(
                command.Code,
                command.Title,
                jobProfileLookup.InternalJobProfileId,
                roleIdResult.Value,
                workCenterIdResult.Value,
                directDependencyResult.Value,
                functionalDependencyResult.Value,
                command.Status,
                command.MaxEmployees,
                command.OccupiedEmployees,
                isFixedTerm,
                command.EffectiveFromUtc,
                command.EffectiveToUtc,
                command.Notes);
        }
        catch (InvalidOperationException exception)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotCommandSupport.MapDomainValidation(exception));
        }

        slot.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(slot);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(slot.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Position slot response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionSlotCreated,
                    AuditEntityTypes.PositionSlot,
                    slot.PublicId,
                    slot.Code,
                    AuditActions.Create,
                    $"Created position slot {slot.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionSlotResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePositionSlotCommandHandler(
    IPositionSlotAuthorizationService authorizationService,
    IPositionSlotRepository repository,
    IIamAdministrationRepository iamRepository,
    ICompanyUserProvisioningService companyUserProvisioningService,
    ICostCenterRepository costCenterRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePositionSlotCommand, PositionSlotResponse>
{
    public async Task<Result<PositionSlotResponse>> Handle(UpdatePositionSlotCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionSlotResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(authorizationResult.Error);
        }

        var slot = await repository.GetByIdAsync(command.PositionSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<PositionSlotResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PositionSlotId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionSlotErrors.PositionSlotNotFound);
        }

        if (slot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(slot.TenantId, command.Code.Trim().ToUpperInvariant(), excludingSlotId: slot.Id, cancellationToken))
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.CodeConflict);
        }

        var jobProfileLookupResult = await PositionSlotCommandSupport.ResolveJobProfileLookupAsync(
            slot.TenantId,
            command.JobProfileId,
            authorizationService,
            repository,
            RbacPermissionAction.Update,
            cancellationToken);
        if (jobProfileLookupResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(jobProfileLookupResult.Error);
        }
        var jobProfileLookup = jobProfileLookupResult.Value;

        var workCenterIdResult = await PositionSlotCommandSupport.ResolveWorkCenterInternalIdAsync(
            slot.TenantId,
            command.WorkCenterId,
            authorizationService,
            repository,
            RbacPermissionAction.Update,
            cancellationToken);
        if (workCenterIdResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(workCenterIdResult.Error);
        }

        var roleIdResult = await PositionSlotCommandSupport.ResolveRoleInternalIdAsync(
            command.RoleId,
            iamRepository,
            cancellationToken);
        if (roleIdResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(roleIdResult.Error);
        }

        if (!jobProfileLookup.OrgUnitId.HasValue)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.JobProfileOrgUnitNotConfigured);
        }

        var isFixedTerm = jobProfileLookup.ContractTypeId.HasValue &&
            PositionSlotContractTypeRules.IsFixedTerm(
            jobProfileLookup.ContractTypeCode,
            jobProfileLookup.ContractTypeName);

        if (!string.IsNullOrWhiteSpace(jobProfileLookup.CostCenterCode) &&
            !await costCenterRepository.ExistsActiveByCodeAsync(
                slot.TenantId,
                jobProfileLookup.CostCenterCode.Trim().ToUpperInvariant(),
                cancellationToken))
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.CostCenterInvalid);
        }

        var before = await repository.GetResponseByIdAsync(slot.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Position slot response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            slot.UpdateCore(
                command.Code,
                command.Title,
                jobProfileLookup.InternalJobProfileId,
                roleIdResult.Value,
                workCenterIdResult.Value,
                command.MaxEmployees,
                isFixedTerm,
                command.EffectiveFromUtc,
                command.EffectiveToUtc,
                command.Notes);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(slot.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Position slot response could not be resolved after update.");

            if (before.RoleId != after.RoleId && after.RoleId.HasValue)
            {
                var syncResult = await companyUserProvisioningService.SyncRoleAssignmentsForPositionSlotAsync(
                    slot.TenantId,
                    slot.PublicId,
                    after.RoleId.Value,
                    cancellationToken);
                if (syncResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<PositionSlotResponse>.Failure(syncResult.Error);
                }

                _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionSlotUpdated,
                    AuditEntityTypes.PositionSlot,
                    slot.PublicId,
                    slot.Code,
                    AuditActions.Update,
                    $"Updated position slot {slot.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionSlotResponse>.Success(after);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PositionSlotResponse>.Failure(PositionSlotCommandSupport.MapDomainValidation(exception));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePositionSlotStatusCommandHandler(
    IPositionSlotAuthorizationService authorizationService,
    IPositionSlotRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePositionSlotStatusCommand, PositionSlotResponse>
{
    public async Task<Result<PositionSlotResponse>> Handle(UpdatePositionSlotStatusCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionSlotResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(authorizationResult.Error);
        }

        var slot = await repository.GetByIdAsync(command.PositionSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<PositionSlotResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PositionSlotId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionSlotErrors.PositionSlotNotFound);
        }

        if (slot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(slot.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Position slot response could not be resolved before status update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            slot.ChangeStatus(command.Status);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(slot.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Position slot response could not be resolved after status update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionSlotStatusChanged,
                    AuditEntityTypes.PositionSlot,
                    slot.PublicId,
                    slot.Code,
                    AuditActions.Update,
                    $"Changed status of position slot {slot.Code} to {slot.Status}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionSlotResponse>.Success(after);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.StatusConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePositionSlotDependenciesCommandHandler(
    IPositionSlotAuthorizationService authorizationService,
    IPositionSlotRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePositionSlotDependenciesCommand, PositionSlotResponse>
{
    public async Task<Result<PositionSlotResponse>> Handle(UpdatePositionSlotDependenciesCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionSlotResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(authorizationResult.Error);
        }

        var slot = await repository.GetByIdAsync(command.PositionSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<PositionSlotResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PositionSlotId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionSlotErrors.PositionSlotNotFound);
        }

        if (slot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.ConcurrencyConflict);
        }

        var directDependencyResult = await PositionSlotCommandSupport.ResolveDependencyInternalIdAsync(
            slot.TenantId,
            command.DirectDependencyPositionSlotId,
            authorizationService,
            repository,
            RbacPermissionAction.Update,
            cancellationToken);
        if (directDependencyResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(directDependencyResult.Error);
        }

        var functionalDependencyResult = await PositionSlotCommandSupport.ResolveDependencyInternalIdAsync(
            slot.TenantId,
            command.FunctionalDependencyPositionSlotId,
            authorizationService,
            repository,
            RbacPermissionAction.Update,
            cancellationToken);
        if (functionalDependencyResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(functionalDependencyResult.Error);
        }

        if (directDependencyResult.Value.HasValue && directDependencyResult.Value.Value == slot.Id)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.DependencySelfReference);
        }

        if (functionalDependencyResult.Value.HasValue && functionalDependencyResult.Value.Value == slot.Id)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.DependencySelfReference);
        }

        if (directDependencyResult.Value.HasValue)
        {
            var graph = await repository.GetGraphNodesAsync(slot.TenantId, cancellationToken);
            var byInternalId = graph.ToDictionary(static node => node.InternalId);
            if (PositionSlotDependencyAnalyzer.WouldCreateDirectCycle(slot.Id, directDependencyResult.Value.Value, byInternalId))
            {
                return Result<PositionSlotResponse>.Failure(PositionSlotErrors.DependencyCycle);
            }
        }

        var before = await repository.GetResponseByIdAsync(slot.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Position slot response could not be resolved before dependency update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            slot.UpdateDependencies(directDependencyResult.Value, functionalDependencyResult.Value);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(slot.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Position slot response could not be resolved after dependency update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionSlotDependencyUpdated,
                    AuditEntityTypes.PositionSlot,
                    slot.PublicId,
                    slot.Code,
                    AuditActions.Update,
                    $"Updated dependencies of position slot {slot.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionSlotResponse>.Success(after);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.DependencySelfReference);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePositionSlotOccupancyCommandHandler(
    IPositionSlotAuthorizationService authorizationService,
    IPositionSlotRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePositionSlotOccupancyCommand, PositionSlotResponse>
{
    public async Task<Result<PositionSlotResponse>> Handle(UpdatePositionSlotOccupancyCommand command, CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PositionSlotResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PositionSlotResponse>.Failure(authorizationResult.Error);
        }

        var slot = await repository.GetByIdAsync(command.PositionSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<PositionSlotResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PositionSlotId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PositionSlotErrors.PositionSlotNotFound);
        }

        if (slot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PositionSlotResponse>.Failure(PositionSlotErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(slot.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Position slot response could not be resolved before occupancy update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            slot.UpdateOccupancy(command.OccupiedEmployees);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(slot.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Position slot response could not be resolved after occupancy update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PositionSlotOccupancyChanged,
                    AuditEntityTypes.PositionSlot,
                    slot.PublicId,
                    slot.Code,
                    AuditActions.Update,
                    $"Updated occupancy of position slot {slot.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PositionSlotResponse>.Success(after);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PositionSlotResponse>.Failure(
                exception.Message.Contains("Suspended", StringComparison.OrdinalIgnoreCase)
                    ? PositionSlotErrors.SuspendedOccupancyConflict
                    : PositionSlotErrors.CapacityRuleViolation);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class PositionSlotPolicyAdapter
{
    public static PositionSlotListItemResponse ApplyAllowedActions(
        PositionSlotListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                PositionSlotPermissionCodes.ResourceKey,
                response.Status.ToString(),
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

    public static PositionSlotResponse ApplyAllowedActions(
        PositionSlotResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                PositionSlotPermissionCodes.ResourceKey,
                response.Status.ToString(),
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
}

internal static class PositionSlotDependencyAnalyzer
{
    public static bool WouldCreateDirectCycle(
        long sourceInternalId,
        long candidateDirectDependencyInternalId,
        IReadOnlyDictionary<long, PositionSlotGraphNodeData> byInternalId)
    {
        var visited = new HashSet<long>();
        long? cursor = candidateDirectDependencyInternalId;

        while (cursor.HasValue)
        {
            if (!visited.Add(cursor.Value))
            {
                return true;
            }

            if (cursor.Value == sourceInternalId)
            {
                return true;
            }

            if (!byInternalId.TryGetValue(cursor.Value, out var node))
            {
                break;
            }

            cursor = node.DirectDependencyInternalId;
        }

        return false;
    }
}

internal static class PositionSlotGraphBuilder
{
    public static PositionSlotGraphResponse Build(
        IReadOnlyCollection<PositionSlotGraphNodeData> nodes,
        Guid? rootId,
        int? depth,
        bool includeFunctional)
    {
        var selected = Select(nodes, rootId, depth, includeFunctional);
        var selectedIds = selected.Select(static node => node.Id).ToHashSet();

        var graphNodes = Order(selected)
            .Select(node => new PositionSlotGraphNodeResponse(
                node.Id,
                node.Code,
                node.Label,
                node.Status,
                node.JobProfileId,
                node.OrgUnitId,
                node.WorkCenterId,
                node.ContractTypeId,
                node.ContractTypeCode,
                node.IsActive))
            .ToArray();

        var edges = new List<PositionSlotGraphEdgeResponse>();
        foreach (var node in selected)
        {
            if (node.DirectDependencyId.HasValue && selectedIds.Contains(node.DirectDependencyId.Value))
            {
                edges.Add(new PositionSlotGraphEdgeResponse(
                    node.DirectDependencyId.Value,
                    node.Id,
                    PositionSlotDependencyRelationType.Direct));
            }

            if (includeFunctional && node.FunctionalDependencyId.HasValue && selectedIds.Contains(node.FunctionalDependencyId.Value))
            {
                edges.Add(new PositionSlotGraphEdgeResponse(
                    node.FunctionalDependencyId.Value,
                    node.Id,
                    PositionSlotDependencyRelationType.Functional));
            }
        }

        var orderedEdges = edges
            .OrderBy(static edge => edge.RelationType)
            .ThenBy(static edge => edge.FromId)
            .ThenBy(static edge => edge.ToId)
            .ToArray();

        return new PositionSlotGraphResponse(graphNodes, orderedEdges);
    }

    private static IReadOnlyCollection<PositionSlotGraphNodeData> Select(
        IReadOnlyCollection<PositionSlotGraphNodeData> nodes,
        Guid? rootId,
        int? depth,
        bool includeFunctional)
    {
        if (!rootId.HasValue && !depth.HasValue)
        {
            return nodes;
        }

        var byId = nodes.ToDictionary(static node => node.Id);
        var byDirectParent = nodes
            .Where(static node => node.DirectDependencyId.HasValue)
            .GroupBy(node => node.DirectDependencyId!.Value)
            .ToDictionary(static group => group.Key, static group => Order(group).ToArray());

        var byFunctionalParent = includeFunctional
            ? nodes
                .Where(static node => node.FunctionalDependencyId.HasValue)
                .GroupBy(node => node.FunctionalDependencyId!.Value)
                .ToDictionary(static group => group.Key, static group => Order(group).ToArray())
            : null;

        var queue = new Queue<(PositionSlotGraphNodeData Node, int Level)>();
        var selected = new Dictionary<Guid, PositionSlotGraphNodeData>();

        if (rootId.HasValue)
        {
            var root = byId[rootId.Value];
            queue.Enqueue((root, 1));
        }
        else
        {
            var roots = nodes.Where(static node => !node.DirectDependencyId.HasValue).ToArray();
            foreach (var root in Order(roots))
            {
                queue.Enqueue((root, 1));
            }
        }

        while (queue.Count > 0)
        {
            var (node, level) = queue.Dequeue();
            if (!selected.TryAdd(node.Id, node))
            {
                continue;
            }

            if (depth.HasValue && level >= depth.Value)
            {
                continue;
            }

            if (byDirectParent.TryGetValue(node.Id, out var directChildren))
            {
                foreach (var child in directChildren)
                {
                    queue.Enqueue((child, level + 1));
                }
            }

            if (includeFunctional && byFunctionalParent is not null && byFunctionalParent.TryGetValue(node.Id, out var functionalChildren))
            {
                foreach (var child in functionalChildren)
                {
                    queue.Enqueue((child, level + 1));
                }
            }
        }

        return selected.Values.ToArray();
    }

    private static IOrderedEnumerable<PositionSlotGraphNodeData> Order(IEnumerable<PositionSlotGraphNodeData> nodes) =>
        nodes
            .OrderBy(static node => node.Label)
            .ThenBy(static node => node.Code);
}

internal static class PositionSlotCommandSupport
{
    public static Error MapDomainValidation(InvalidOperationException exception)
    {
        if (exception.Message.Contains("date", StringComparison.OrdinalIgnoreCase))
        {
            return PositionSlotErrors.EffectiveDatesInvalid;
        }

        if (exception.Message.Contains("OccupiedEmployees", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("MaxEmployees", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("occupancy", StringComparison.OrdinalIgnoreCase))
        {
            return PositionSlotErrors.CapacityRuleViolation;
        }

        if (exception.Message.Contains("Vacant", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("Occupied status", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("status", StringComparison.OrdinalIgnoreCase))
        {
            return PositionSlotErrors.StatusConflict;
        }

        return PositionSlotErrors.CapacityRuleViolation;
    }

    public static async Task<Result<PositionSlotJobProfileLookup>> ResolveJobProfileLookupAsync(
        Guid tenantId,
        Guid jobProfileId,
        IPositionSlotAuthorizationService authorizationService,
        IPositionSlotRepository repository,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        var lookup = await repository.GetJobProfileLookupAsync(tenantId, jobProfileId, cancellationToken);
        if (lookup is not null)
        {
            return Result<PositionSlotJobProfileLookup>.Success(lookup);
        }

        return Result<PositionSlotJobProfileLookup>.Failure(
            await repository.JobProfileExistsOutsideTenantAsync(jobProfileId, cancellationToken)
                ? authorizationService.TenantMismatch(action)
                : PositionSlotErrors.JobProfileNotFound);
    }

    public static async Task<Result<long?>> ResolveWorkCenterInternalIdAsync(
        Guid tenantId,
        Guid? workCenterId,
        IPositionSlotAuthorizationService authorizationService,
        IPositionSlotRepository repository,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (!workCenterId.HasValue)
        {
            return Result<long?>.Success(null);
        }

        var internalId = await repository.ResolveWorkCenterIdAsync(tenantId, workCenterId.Value, cancellationToken);
        if (internalId.HasValue)
        {
            return Result<long?>.Success(internalId.Value);
        }

        return Result<long?>.Failure(
            await repository.WorkCenterExistsOutsideTenantAsync(workCenterId.Value, cancellationToken)
                ? authorizationService.TenantMismatch(action)
                : PositionSlotErrors.WorkCenterNotFound);
    }

    public static async Task<Result<long?>> ResolveRoleInternalIdAsync(
        Guid? roleId,
        IIamAdministrationRepository iamRepository,
        CancellationToken cancellationToken)
    {
        if (!roleId.HasValue)
        {
            return Result<long?>.Success(null);
        }

        var role = await iamRepository.FindRoleByPublicIdAsync(roleId.Value, includePermissions: false, cancellationToken);
        return role is null
            ? Result<long?>.Failure(PositionSlotErrors.RoleNotFound)
            : Result<long?>.Success(role.Id);
    }

    public static async Task<Result<long?>> ResolveDependencyInternalIdAsync(
        Guid tenantId,
        Guid? dependencySlotId,
        IPositionSlotAuthorizationService authorizationService,
        IPositionSlotRepository repository,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (!dependencySlotId.HasValue)
        {
            return Result<long?>.Success(null);
        }

        var internalId = await repository.ResolvePositionSlotIdAsync(tenantId, dependencySlotId.Value, cancellationToken);
        if (internalId.HasValue)
        {
            return Result<long?>.Success(internalId.Value);
        }

        return Result<long?>.Failure(
            await repository.ExistsOutsideTenantAsync(dependencySlotId.Value, cancellationToken)
                ? authorizationService.TenantMismatch(action)
                : PositionSlotErrors.DependencyNotFound);
    }
}
