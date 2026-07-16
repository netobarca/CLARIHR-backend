using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Payroll.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Payroll;

namespace CLARIHR.Application.Features.Payroll;

internal sealed class SearchPayrollDefinitionsQueryHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IPayrollDefinitionRepository repository,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<SearchPayrollDefinitionsQuery, PagedResponse<PayrollDefinitionListItemResponse>>
{
    public async Task<Result<PagedResponse<PayrollDefinitionListItemResponse>>> Handle(
        SearchPayrollDefinitionsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PayrollDefinitionListItemResponse>>.Failure(authorizationResult.Error);
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
            return Result<PagedResponse<PayrollDefinitionListItemResponse>>.Success(response);
        }

        var canManage = (await authorizationService.EnsureCanManageAsync(query.CompanyId, cancellationToken)).IsSuccess;
        var items = response.Items
            .Select(item => PayrollDefinitionPolicyAdapter.ApplyAllowedActions(item, resourceActionPolicyService, canManage))
            .ToArray();
        response = response with { Items = items };

        return Result<PagedResponse<PayrollDefinitionListItemResponse>>.Success(response);
    }
}

internal sealed class GetPayrollDefinitionByIdQueryHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IPayrollDefinitionRepository repository,
    ITenantContext tenantContext,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<GetPayrollDefinitionByIdQuery, PayrollDefinitionResponse>
{
    public async Task<Result<PayrollDefinitionResponse>> Handle(
        GetPayrollDefinitionByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollDefinitionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollDefinitionResponse>.Failure(authorizationResult.Error);
        }

        var response = await repository.GetResponseByIdAsync(query.PayrollDefinitionId, cancellationToken);
        if (response is not null)
        {
            var canManage = (await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
            response = PayrollDefinitionPolicyAdapter.ApplyAllowedActions(response, resourceActionPolicyService, canManage);

            return Result<PayrollDefinitionResponse>.Success(response);
        }

        return Result<PayrollDefinitionResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(query.PayrollDefinitionId, cancellationToken)
                ? PayrollDefinitionErrors.TenantMismatch(RbacPermissionAction.Read)
                : PayrollDefinitionErrors.PayrollDefinitionNotFound);
    }
}

internal sealed class CreatePayrollDefinitionCommandHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IPayrollDefinitionRepository repository,
    IPersonnelFileRepository personnelFileRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePayrollDefinitionCommand, PayrollDefinitionResponse>
{
    public async Task<Result<PayrollDefinitionResponse>> Handle(
        CreatePayrollDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollDefinitionResponse>.Failure(authorizationResult.Error);
        }

        var catalogError = await PayrollDefinitionCatalogProbe.ValidateAsync(
            personnelFileRepository,
            command.CompanyId,
            command.PayrollTypeCode,
            command.PayPeriodCode,
            command.CurrencyCode,
            cancellationToken);
        if (catalogError is not null)
        {
            return Result<PayrollDefinitionResponse>.Failure(catalogError);
        }

        if (await repository.CodeExistsAsync(
                command.CompanyId,
                command.Code.Trim().ToUpperInvariant(),
                excludingPayrollDefinitionId: null,
                cancellationToken))
        {
            return Result<PayrollDefinitionResponse>.Failure(PayrollDefinitionErrors.CodeTaken);
        }

        var definition = PayrollDefinition.Create(
            command.Code,
            command.Name,
            command.PayrollTypeCode,
            command.PayPeriodCode,
            command.TotalPeriods,
            command.GuaranteesMinimumIncome,
            command.CurrencyCode,
            command.OvertimeWindowEnabled,
            command.OvertimeWindowOffsetDays,
            command.AttendanceWindowEnabled,
            command.AttendanceWindowOffsetDays);
        definition.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(definition);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await repository.GetResponseByIdAsync(definition.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll definition response could not be resolved after creation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollDefinitionCreated,
                    AuditEntityTypes.PayrollDefinition,
                    definition.PublicId,
                    definition.Code,
                    AuditActions.Create,
                    $"Created payroll definition {definition.Code}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollDefinitionResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException ex) when (PayrollDefinitionConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollDefinitionResponse>.Failure(PayrollDefinitionErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePayrollDefinitionCommandHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IPayrollDefinitionRepository repository,
    IPersonnelFileRepository personnelFileRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdatePayrollDefinitionCommand, PayrollDefinitionResponse>
{
    public async Task<Result<PayrollDefinitionResponse>> Handle(
        UpdatePayrollDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollDefinitionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollDefinitionResponse>.Failure(authorizationResult.Error);
        }

        var definition = await repository.GetByIdAsync(command.PayrollDefinitionId, cancellationToken);
        if (definition is null)
        {
            return Result<PayrollDefinitionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PayrollDefinitionId, cancellationToken)
                    ? PayrollDefinitionErrors.TenantMismatch(RbacPermissionAction.Update)
                    : PayrollDefinitionErrors.PayrollDefinitionNotFound);
        }

        if (definition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PayrollDefinitionResponse>.Failure(PayrollDefinitionErrors.ConcurrencyConflict);
        }

        var catalogError = await PayrollDefinitionCatalogProbe.ValidateAsync(
            personnelFileRepository,
            definition.TenantId,
            command.PayrollTypeCode,
            command.PayPeriodCode,
            command.CurrencyCode,
            cancellationToken);
        if (catalogError is not null)
        {
            return Result<PayrollDefinitionResponse>.Failure(catalogError);
        }

        if (await repository.CodeExistsAsync(
                definition.TenantId,
                command.Code.Trim().ToUpperInvariant(),
                definition.PublicId,
                cancellationToken))
        {
            return Result<PayrollDefinitionResponse>.Failure(PayrollDefinitionErrors.CodeTaken);
        }

        var before = await repository.GetResponseByIdAsync(definition.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Payroll definition response could not be resolved before update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            definition.Update(
                command.Code,
                command.Name,
                command.PayrollTypeCode,
                command.PayPeriodCode,
                command.TotalPeriods,
                command.GuaranteesMinimumIncome,
                command.CurrencyCode,
                command.OvertimeWindowEnabled,
                command.OvertimeWindowOffsetDays,
                command.AttendanceWindowEnabled,
                command.AttendanceWindowOffsetDays);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(definition.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll definition response could not be resolved after update.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollDefinitionUpdated,
                    AuditEntityTypes.PayrollDefinition,
                    definition.PublicId,
                    definition.Code,
                    AuditActions.Update,
                    $"Updated payroll definition {definition.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollDefinitionResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (PayrollDefinitionConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollDefinitionResponse>.Failure(PayrollDefinitionErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ActivatePayrollDefinitionCommandHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IPayrollDefinitionRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivatePayrollDefinitionCommand, PayrollDefinitionResponse>
{
    public async Task<Result<PayrollDefinitionResponse>> Handle(
        ActivatePayrollDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollDefinitionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollDefinitionResponse>.Failure(authorizationResult.Error);
        }

        var definition = await repository.GetByIdAsync(command.PayrollDefinitionId, cancellationToken);
        if (definition is null)
        {
            return Result<PayrollDefinitionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PayrollDefinitionId, cancellationToken)
                    ? PayrollDefinitionErrors.TenantMismatch(RbacPermissionAction.Update)
                    : PayrollDefinitionErrors.PayrollDefinitionNotFound);
        }

        if (definition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PayrollDefinitionResponse>.Failure(PayrollDefinitionErrors.ConcurrencyConflict);
        }

        var before = await repository.GetResponseByIdAsync(definition.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Payroll definition response could not be resolved before activation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            definition.Activate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(definition.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll definition response could not be resolved after activation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollDefinitionActivated,
                    AuditEntityTypes.PayrollDefinition,
                    definition.PublicId,
                    definition.Code,
                    AuditActions.Reactivate,
                    $"Activated payroll definition {definition.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollDefinitionResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (PayrollDefinitionConstraintViolations.IsCodeConflict(ex.ConstraintName))
        {
            // The filtered (tenant, normalized_code) WHERE is_active unique index means reactivating a
            // definition whose code is already taken by an active one trips the index — map to a clean 409.
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollDefinitionResponse>.Failure(PayrollDefinitionErrors.CodeTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class InactivatePayrollDefinitionCommandHandler(
    IPayrollConfigurationAuthorizationService authorizationService,
    IPayrollDefinitionRepository repository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<InactivatePayrollDefinitionCommand, PayrollDefinitionResponse>
{
    public async Task<Result<PayrollDefinitionResponse>> Handle(
        InactivatePayrollDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PayrollDefinitionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollDefinitionResponse>.Failure(authorizationResult.Error);
        }

        var definition = await repository.GetByIdAsync(command.PayrollDefinitionId, cancellationToken);
        if (definition is null)
        {
            return Result<PayrollDefinitionResponse>.Failure(
                await repository.ExistsOutsideTenantAsync(command.PayrollDefinitionId, cancellationToken)
                    ? PayrollDefinitionErrors.TenantMismatch(RbacPermissionAction.Update)
                    : PayrollDefinitionErrors.PayrollDefinitionNotFound);
        }

        if (definition.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PayrollDefinitionResponse>.Failure(PayrollDefinitionErrors.ConcurrencyConflict);
        }

        // Logical-delete usage guard (PAYROLL_DEFINITION_IN_USE). PR-1 has neither the period FK (M2/PR-2)
        // nor the run table (M4/PR-4) yet, so IsInUseAsync is a stub returning false today; the real
        // reference probes are wired as those PRs land.
        if (await repository.IsInUseAsync(definition.TenantId, definition.PublicId, cancellationToken))
        {
            return Result<PayrollDefinitionResponse>.Failure(PayrollDefinitionErrors.InUse);
        }

        var before = await repository.GetResponseByIdAsync(definition.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Payroll definition response could not be resolved before inactivation.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            definition.Inactivate();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetResponseByIdAsync(definition.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll definition response could not be resolved after inactivation.");

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollDefinitionInactivated,
                    AuditEntityTypes.PayrollDefinition,
                    definition.PublicId,
                    definition.Code,
                    AuditActions.Deactivate,
                    $"Inactivated payroll definition {definition.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollDefinitionResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

/// <summary>
/// The three catalog references of the master must be ACTIVE codes of their country catalogs (REQ-012
/// §3.1 — reuses the <c>CatalogCodeIsActiveAsync</c> cases PAYROLLTYPE / PAYPERIOD / CURRENCY). One shared
/// probe for create/update; any miss maps to the single §5 code <c>PAYROLL_DEFINITION_CATALOG_INVALID</c>.
/// </summary>
internal static class PayrollDefinitionCatalogProbe
{
    public static async Task<Error?> ValidateAsync(
        IPersonnelFileRepository personnelFileRepository,
        Guid companyId,
        string payrollTypeCode,
        string payPeriodCode,
        string currencyCode,
        CancellationToken cancellationToken)
    {
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                companyId,
                PersonnelCurriculumCatalogCategories.PayrollType,
                payrollTypeCode,
                cancellationToken))
        {
            return PayrollDefinitionErrors.CatalogInvalid;
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                companyId,
                PersonnelCurriculumCatalogCategories.PayPeriod,
                payPeriodCode,
                cancellationToken))
        {
            return PayrollDefinitionErrors.CatalogInvalid;
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                companyId,
                PersonnelCurriculumCatalogCategories.Currency,
                currencyCode,
                cancellationToken))
        {
            return PayrollDefinitionErrors.CatalogInvalid;
        }

        return null;
    }
}

internal static class PayrollDefinitionConstraintViolations
{
    // The filtered (TenantId, NormalizedCode) WHERE is_active unique index is the real guard against
    // duplicate active codes; the up-front CodeExistsAsync probe only closes the common (sequential) case.
    // On a concurrent create/update/activate of the same code, the second writer trips this index — map it
    // to the same clean 409 as the probe.
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, PayrollMasterConstraintNames.PayrollDefinitionCodeUnique, StringComparison.Ordinal);
}

internal static class PayrollDefinitionPolicyAdapter
{
    public static PayrollDefinitionListItemResponse ApplyAllowedActions(
        PayrollDefinitionListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    public static PayrollDefinitionResponse ApplyAllowedActions(
        PayrollDefinitionResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        response with { AllowedActions = Evaluate(response.IsActive, resourceActionPolicyService, canManage) };

    private static AllowedActionsResponse Evaluate(
        bool isActive,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage) =>
        resourceActionPolicyService.Evaluate(
            new ResourceActionContext(
                PayrollConfigurationPermissionCodes.PayrollDefinitionsResourceKey,
                State: null,
                isActive,
                SupportsEdit: true,
                EditAllowed: canManage,
                SupportsDelete: false,
                SupportsArchive: false,
                SupportsActivate: true,
                ActivateAllowed: canManage,
                SupportsInactivate: true,
                InactivateAllowed: canManage));
}
