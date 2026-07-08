using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Features.Leave;

internal sealed class SearchIncapacityRisksQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityRiskRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchIncapacityRisksQuery, PagedResponse<IncapacityRiskListItemResponse>>
{
    public async Task<Result<PagedResponse<IncapacityRiskListItemResponse>>> Handle(
        SearchIncapacityRisksQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<IncapacityRiskListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await repository.SearchAsync(
            query.CompanyId,
            query.IsActive,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (!query.IncludeAllowedActions)
        {
            return Result<PagedResponse<IncapacityRiskListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => IncapacityRiskPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<IncapacityRiskListItemResponse>>.Success(response);
    }
}

internal sealed class GetIncapacityRiskByIdQueryHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityRiskRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetIncapacityRiskByIdQuery, IncapacityRiskResponse>
{
    public async Task<Result<IncapacityRiskResponse>> Handle(
        GetIncapacityRiskByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IncapacityRiskResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityRiskResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.IncapacityRiskId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = IncapacityRiskPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<IncapacityRiskResponse>.Success(response);
        }

        return Result<IncapacityRiskResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.IncapacityRiskId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : IncapacityRiskErrors.IncapacityRiskNotFound);
    }
}

internal sealed class CreateIncapacityRiskCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityRiskRepository repository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateIncapacityRiskCommand, IncapacityRiskResponse>
{
    public async Task<Result<IncapacityRiskResponse>> Handle(
        CreateIncapacityRiskCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityRiskResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Code.Trim().ToUpperInvariant(),
                excludingIncapacityRiskId: null,
                cancellationToken))
        {
            return Result<IncapacityRiskResponse>.Failure(IncapacityRiskErrors.CodeConflict);
        }

        IncapacityRisk incapacityRisk;
        try
        {
            incapacityRisk = IncapacityRisk.Create(
                command.Code,
                command.Name,
                command.CountsSeventhDay,
                command.CountsSaturday,
                command.CountsHoliday,
                command.UsesWorkSchedule,
                command.AllowsIndefinite,
                command.AllowsExtension,
                command.UsesFund,
                command.HasSubsidy);

            // An empty set is a valid creation state even with subsidy (tranches can be defined
            // later via PUT …/parameters), so ReplaceParameters — whose guard demands at least one
            // tranche when HasSubsidy — only runs when tranches actually travel in the POST.
            if (command.Parameters.Count > 0)
            {
                incapacityRisk.ReplaceParameters(IncapacityRiskParameterMapping.ToDomainInputs(command.Parameters));
            }
        }
        catch (ArgumentException ex)
        {
            // Covers ArgumentOutOfRangeException too (it derives from ArgumentException): the
            // domain guard rejected the tranche set (contiguity, day-1 start, payer code, …).
            return Result<IncapacityRiskResponse>.Failure(IncapacityRiskErrors.ParametersInvalid(ex.Message));
        }

        incapacityRisk.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(incapacityRisk);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(incapacityRisk.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity risk response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.IncapacityRiskCreated,
                    AuditEntityTypes.IncapacityRisk,
                    incapacityRisk.PublicId,
                    incapacityRisk.Code,
                    AuditActions.Create,
                    $"Created incapacity risk {incapacityRisk.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<IncapacityRiskResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (IncapacityRiskConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<IncapacityRiskResponse>.Failure(IncapacityRiskErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateIncapacityRiskCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityRiskRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateIncapacityRiskCommand, IncapacityRiskResponse>
{
    public async Task<Result<IncapacityRiskResponse>> Handle(
        UpdateIncapacityRiskCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IncapacityRiskResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityRiskResponse>.Failure(authorizationResult.Error);
        }

        var incapacityRisk = await repository.GetByIdAsync(command.IncapacityRiskId, cancellationToken);
        if (incapacityRisk is null)
        {
            return Result<IncapacityRiskResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.IncapacityRiskId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : IncapacityRiskErrors.IncapacityRiskNotFound);
        }

        if (incapacityRisk.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<IncapacityRiskResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        if (await repository.CodeExistsAsync(
                incapacityRisk.TenantId,
                command.Code.Trim().ToUpperInvariant(),
                incapacityRisk.PublicId,
                cancellationToken))
        {
            return Result<IncapacityRiskResponse>.Failure(IncapacityRiskErrors.CodeConflict);
        }

        var before = await repository.GetResponseByIdAsync(incapacityRisk.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Incapacity risk response could not be resolved before update.");

        try
        {
            // The guard (cannot turn off HasSubsidy while parameters exist) throws before mutating
            // any state, so the tracked aggregate stays clean when we surface the 422.
            incapacityRisk.Update(
                command.Code,
                command.Name,
                command.CountsSeventhDay,
                command.CountsSaturday,
                command.CountsHoliday,
                command.UsesWorkSchedule,
                command.AllowsIndefinite,
                command.AllowsExtension,
                command.UsesFund,
                command.HasSubsidy);
        }
        catch (ArgumentException ex)
        {
            return Result<IncapacityRiskResponse>.Failure(IncapacityRiskErrors.RuleViolation(ex.Message));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(incapacityRisk.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity risk response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.IncapacityRiskUpdated,
                    AuditEntityTypes.IncapacityRisk,
                    incapacityRisk.PublicId,
                    incapacityRisk.Code,
                    AuditActions.Update,
                    $"Updated incapacity risk {incapacityRisk.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<IncapacityRiskResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (IncapacityRiskConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<IncapacityRiskResponse>.Failure(IncapacityRiskErrors.CodeConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ReplaceIncapacityRiskParametersCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityRiskRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReplaceIncapacityRiskParametersCommand, IncapacityRiskResponse>
{
    public async Task<Result<IncapacityRiskResponse>> Handle(
        ReplaceIncapacityRiskParametersCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IncapacityRiskResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityRiskResponse>.Failure(authorizationResult.Error);
        }

        var incapacityRisk = await repository.GetByIdAsync(command.IncapacityRiskId, cancellationToken);
        if (incapacityRisk is null)
        {
            return Result<IncapacityRiskResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.IncapacityRiskId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : IncapacityRiskErrors.IncapacityRiskNotFound);
        }

        if (incapacityRisk.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<IncapacityRiskResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(incapacityRisk.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Incapacity risk response could not be resolved before replacing parameters.");

        try
        {
            // The domain validates the whole tranche set before touching the child collection, so
            // the tracked aggregate stays clean when we surface the 422.
            incapacityRisk.ReplaceParameters(IncapacityRiskParameterMapping.ToDomainInputs(command.Parameters));
        }
        catch (ArgumentException ex)
        {
            // Covers ArgumentOutOfRangeException too (it derives from ArgumentException).
            return Result<IncapacityRiskResponse>.Failure(IncapacityRiskErrors.ParametersInvalid(ex.Message));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(incapacityRisk.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity risk response could not be resolved after replacing parameters.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.IncapacityRiskParametersReplaced,
                    AuditEntityTypes.IncapacityRisk,
                    incapacityRisk.PublicId,
                    incapacityRisk.Code,
                    AuditActions.Update,
                    $"Replaced subsidy parameters of incapacity risk {incapacityRisk.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<IncapacityRiskResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivateIncapacityRiskCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityRiskRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateIncapacityRiskCommand, IncapacityRiskResponse>
{
    public async Task<Result<IncapacityRiskResponse>> Handle(
        ActivateIncapacityRiskCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IncapacityRiskResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityRiskResponse>.Failure(authorizationResult.Error);
        }

        var incapacityRisk = await repository.GetByIdAsync(command.IncapacityRiskId, cancellationToken);
        if (incapacityRisk is null)
        {
            return Result<IncapacityRiskResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.IncapacityRiskId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : IncapacityRiskErrors.IncapacityRiskNotFound);
        }

        if (incapacityRisk.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<IncapacityRiskResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(incapacityRisk.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Incapacity risk response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            incapacityRisk.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(incapacityRisk.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity risk response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.IncapacityRiskActivated,
                    AuditEntityTypes.IncapacityRisk,
                    incapacityRisk.PublicId,
                    incapacityRisk.Code,
                    AuditActions.Reactivate,
                    $"Activated incapacity risk {incapacityRisk.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<IncapacityRiskResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivateIncapacityRiskCommandHandler(
    ILeaveConfigurationAuthorizationService authorizationService,
    IIncapacityRiskRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivateIncapacityRiskCommand, IncapacityRiskResponse>
{
    public async Task<Result<IncapacityRiskResponse>> Handle(
        InactivateIncapacityRiskCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<IncapacityRiskResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityRiskResponse>.Failure(authorizationResult.Error);
        }

        var incapacityRisk = await repository.GetByIdAsync(command.IncapacityRiskId, cancellationToken);
        if (incapacityRisk is null)
        {
            return Result<IncapacityRiskResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.IncapacityRiskId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : IncapacityRiskErrors.IncapacityRiskNotFound);
        }

        if (incapacityRisk.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<IncapacityRiskResponse>.Failure(LeaveConfigurationErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(incapacityRisk.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Incapacity risk response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            incapacityRisk.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(incapacityRisk.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity risk response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.IncapacityRiskInactivated,
                    AuditEntityTypes.IncapacityRisk,
                    incapacityRisk.PublicId,
                    incapacityRisk.Code,
                    AuditActions.Deactivate,
                    $"Inactivated incapacity risk {incapacityRisk.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<IncapacityRiskResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class IncapacityRiskParameterMapping
{
    public static IReadOnlyCollection<IncapacityRiskParameterInput> ToDomainInputs(
        IReadOnlyCollection<IncapacityRiskParameterInputModel> parameters) =>
        parameters
            .Select(parameter => new IncapacityRiskParameterInput(
                parameter.DayFrom,
                parameter.DayTo,
                parameter.SubsidyPercent,
                parameter.PayerCode))
            .ToArray();
}

internal static class IncapacityRiskConstraintViolations
{
    // The (TenantId, NormalizedCode) unique index is the real guard against duplicate codes; the
    // up-front CodeExistsAsync probe only closes the common (sequential) case. On a concurrent
    // create/update of the same code, the second writer trips this index — map it to the same
    // clean 409 as the probe instead of letting the 23505 escape as an HTTP 500 (mirrors
    // MedicalClinicConstraintViolations).
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, LeaveMasterConstraintNames.IncapacityRiskCodeUnique, StringComparison.Ordinal);
}

internal static class IncapacityRiskPolicyAdapter
{
    public static IncapacityRiskListItemResponse ApplyAllowedActions(
        IncapacityRiskListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.IncapacityRisksResourceKey,
                State: null,
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

    public static IncapacityRiskResponse ApplyAllowedActions(
        IncapacityRiskResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                LeaveConfigurationPermissionCodes.IncapacityRisksResourceKey,
                State: null,
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
