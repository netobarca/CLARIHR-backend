using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal abstract class PersonnelFileEmployeeCommandValidatorBase<TCommand, TItem> : AbstractValidator<TCommand>
{
    protected void Configure(GuidAccessor<TCommand> idAccessor, GuidAccessor<TCommand> concurrencyAccessor, CollectionAccessor<TCommand, TItem> itemsAccessor, IValidator<TItem> itemValidator)
    {
        RuleFor(idAccessor.Accessor).NotEmpty();
        RuleFor(concurrencyAccessor.Accessor).NotEmpty();
        RuleFor(itemsAccessor.Accessor).NotNull();
        RuleForEach(itemsAccessor.Accessor).SetValidator(itemValidator);
    }

    protected readonly record struct GuidAccessor<T>(global::System.Linq.Expressions.Expression<Func<T, Guid>> Accessor);
    protected readonly record struct CollectionAccessor<T, TCollectionItem>(global::System.Linq.Expressions.Expression<Func<T, IEnumerable<TCollectionItem>>> Accessor);
}

internal static class PersonnelFileEmployeeAudits
{
    public static async Task LogUpdateAsync(
        IAuditService auditService,
        PersonnelFile personnelFile,
        string summary,
        object? after,
        CancellationToken cancellationToken) =>
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.PersonnelFileUpdated,
                AuditEntityTypes.PersonnelFile,
                personnelFile.PublicId,
                personnelFile.FullName,
                AuditActions.Update,
                summary,
                Before: null,
                After: after),
            cancellationToken);

    /// <summary>
    /// Overload that records the previous state as well, so the audit entry carries a before/after diff
    /// (e.g. insurance edits/deletions — D-15). Used by update/patch/delete handlers; create keeps the
    /// before-less overload.
    /// </summary>
    public static async Task LogUpdateAsync(
        IAuditService auditService,
        PersonnelFile personnelFile,
        string summary,
        object? before,
        object? after,
        CancellationToken cancellationToken) =>
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.PersonnelFileUpdated,
                AuditEntityTypes.PersonnelFile,
                personnelFile.PublicId,
                personnelFile.FullName,
                AuditActions.Update,
                summary,
                Before: before,
                After: after),
            cancellationToken);
}

internal abstract class PersonnelFileEmployeeCommandHandlerBase
{
    protected static PersonnelFileSectionResult<TSection> CreateSectionResult<TSection>(
        PersonnelFile personnelFile,
        TSection data) =>
        new(data, personnelFile.ConcurrencyToken, personnelFile.ModifiedUtc);

    protected static PersonnelFileSectionResult CreateSectionResult(PersonnelFile personnelFile) =>
        new(personnelFile.ConcurrencyToken, personnelFile.ModifiedUtc);

    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        // Employee sub-resource writes enforce optimistic concurrency at the item level (each
        // command carries the item's own If-Match token), not on the parent file — so callers
        // pass Guid.Empty to opt out of a parent-file concurrency check here. Only enforce the
        // parent token when a caller actually supplies one; a non-empty file token must never be
        // compared against the Guid.Empty sentinel (that produced a spurious 409 on every write).
        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Manage gate for authorization substitutions (D-09): identical to <see cref="LoadForManageAsync{TResponse}"/>
    /// but enforces the dedicated <c>PersonnelFiles.ManageSubstitutions</c> permission instead of the generic
    /// manage gate. Item-level optimistic concurrency is enforced by each command's own If-Match token, so
    /// callers pass <see cref="Guid.Empty"/> to opt out of the parent-file concurrency check here.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageSubstitutionsAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageSubstitutionsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Manage gate for medical claims (D-08): identical to <see cref="LoadForManageSubstitutionsAsync{TResponse}"/>
    /// but enforces the dedicated <c>PersonnelFiles.ManageMedicalClaims</c> permission. Used by update/patch/delete
    /// (third-party management only — no self-service). Item-level optimistic concurrency is enforced by each
    /// command's own If-Match token, so callers pass <see cref="Guid.Empty"/> here.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageMedicalClaimsAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageMedicalClaimsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Create gate for medical claims (D-09 self-service): the caller has the dedicated
    /// <c>PersonnelFiles.ManageMedicalClaims</c> permission (or Admin), OR the caller is the employee
    /// registering a claim on their own file (self-service). This is the only self-service write path in
    /// the codebase; edits/deletions remain manager-only (see <see cref="LoadForManageMedicalClaimsAsync{TResponse}"/>).
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForCreateOwnOrManageMedicalClaimAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canManageByRole = (await authorizationService.EnsureCanManageMedicalClaimsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canManageByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Manage gate for incapacities (vacaciones/incapacidades D-17/D-18): the dedicated
    /// <c>PersonnelFiles.ManageIncapacities</c> permission (or Admin). Used by confirm/close/annul/extension and
    /// the HR edit — no self-service. Item-level optimistic concurrency is enforced by each command's own
    /// If-Match token, so callers pass <see cref="Guid.Empty"/> here.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageIncapacitiesAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageIncapacitiesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Create gate for incapacities (D-18 self-service): the caller has the dedicated
    /// <c>PersonnelFiles.ManageIncapacities</c> permission (or Admin) — HR registers a REGISTRADA record — OR
    /// the caller is the employee self-registering an incapacity on their own file (EN_REVISION until HR
    /// confirms). Returns <c>IsManager</c> so the handler derives the origin (RRHH vs AUTOSERVICIO) and the
    /// initial status from the branch that authorized the write.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File, bool IsManager)> LoadForCreateOwnOrManageIncapacityAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null, false);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null,
                false);
        }

        var canManageByRole = (await authorizationService.EnsureCanManageIncapacitiesAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canManageByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null, false);
            }
        }

        return (null, personnelFile, canManageByRole);
    }

    /// <summary>
    /// Manage gate for compensatory time (REQ-002 D-01): the dedicated
    /// <c>PersonnelFiles.ManageCompensatoryTime</c> permission (or Admin). Used by every credit/absence write —
    /// F1 is HR-only, no self-service (D-01). Item-level optimistic concurrency is enforced by each command's own
    /// If-Match token, so callers pass <see cref="Guid.Empty"/> here.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageCompensatoryTimeAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageCompensatoryTimeAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Manage gate for vacations (leave module D-17): the dedicated <c>PersonnelFiles.ManageVacations</c>
    /// permission (or Admin). Used by the fund CRUD and the mass generation — no self-service. Item-level
    /// optimistic concurrency is enforced by each command's own If-Match token, so callers pass
    /// <see cref="Guid.Empty"/> here.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageVacationsAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageVacationsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for vacation sub-resources (leave module D-17/D-18): the caller has the dedicated
    /// <c>ViewVacations</c> permission (or Admin), OR the caller is the employee reading their own fund /
    /// requests (self-service). Without the permission and without being the owner the caller gets 403 (no
    /// masking). Requires a completed employee.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForVacationReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canViewByRole = (await authorizationService.EnsureCanViewVacationsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canViewByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Create/cancel gate for vacation requests (leave module D-18 self-service): the caller has the dedicated
    /// <c>PersonnelFiles.ManageVacations</c> permission (or Admin) — HR raises a request on any file — OR the
    /// caller is the employee acting on their own file (self-service create / self-cancel of a SOLICITADA
    /// request). Deciding/returning stays manager-only with the anti-self guard
    /// (see <see cref="LoadForManageVacationsAsync{TResponse}"/>). Returns <c>IsManager</c> so the handler derives
    /// the requester snapshot (RRHH vs the subject employee).
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File, bool IsManager)> LoadForCreateOwnOrManageVacationRequestAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null, false);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null,
                false);
        }

        var canManageByRole = (await authorizationService.EnsureCanManageVacationsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canManageByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null, false);
            }
        }

        return (null, personnelFile, canManageByRole);
    }

    /// <summary>
    /// Manage gate for economic-aid requests (D-03): the dedicated <c>PersonnelFiles.ManageEconomicAidRequests</c>
    /// permission (or Admin). Used by validate (resolution)/disburse/update/delete — RR. HH. only, no self-service.
    /// Item-level optimistic concurrency is enforced by each command's own If-Match token, so callers pass
    /// <see cref="Guid.Empty"/> here.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageEconomicAidRequestsAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageEconomicAidRequestsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Create/cancel gate for economic-aid requests (D-02 self-service): the caller has the dedicated
    /// <c>PersonnelFiles.ManageEconomicAidRequests</c> permission (or Admin), OR the caller is the employee
    /// acting on their own file (self-service create / self-cancel of a pending request, D-11). Validation
    /// (resolution/disburse) stays manager-only (see <see cref="LoadForManageEconomicAidRequestsAsync{TResponse}"/>).
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForCreateOwnOrManageEconomicAidAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canManageByRole = (await authorizationService.EnsureCanManageEconomicAidRequestsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canManageByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for economic-aid sub-resources (D-10): the caller has the dedicated
    /// <c>ViewEconomicAidRequests</c> permission (or Admin), OR the caller is the employee reading their own
    /// requests (self-service, D-02). The emergency reason is sensitive: without the permission and without being
    /// the owner the caller gets 403. Requires a completed employee.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForEconomicAidReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canViewByRole = (await authorizationService.EnsureCanViewEconomicAidRequestsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canViewByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

    /// <summary>Read gate of the settlement module (D-20): ViewSettlements / Admin / super-admin, per-handler.</summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForViewSettlementsAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanViewSettlementsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Manage gate of the settlement module (D-20): the dedicated <c>PersonnelFiles.ManageSettlements</c>
    /// permission (or Admin). Item-level optimistic concurrency is enforced by each command's own If-Match
    /// token.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageSettlementsAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageSettlementsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Manage gate for retirement requests (D-12): the dedicated <c>PersonnelFiles.ManageRetirements</c>
    /// permission (or Admin). Register/edit/cancel(SOLICITADA)/execute — RRHH only, no self-service (D-03).
    /// Item-level optimistic concurrency is enforced by each command's own If-Match token, so callers pass
    /// <see cref="Guid.Empty"/> here.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageRetirementsAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageRetirementsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Gate for the AUTHORIZER actions on a retirement request (resolution / annulment of an AUTORIZADA —
    /// D-12/D-13): the dedicated <c>PersonnelFiles.AuthorizeRetirement</c> permission or the IAM super-admin.
    /// <c>PersonnelFiles.Admin</c> is deliberately EXCLUDED (separation of duties, mirrors AuthorizeRehire).
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForAuthorizeRetirementAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanAuthorizeRetirementAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Gate for REVERTING an executed retirement (D-12): the dedicated <c>PersonnelFiles.RevertRetirement</c>
    /// permission or the IAM super-admin. <c>PersonnelFiles.Admin</c> is deliberately EXCLUDED.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForRevertRetirementAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanRevertRetirementAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for retirement requests (D-12): the dedicated <c>PersonnelFiles.ViewRetirements</c>
    /// permission (or Admin). RRHH-only — no self-service read in Fase 1 (D-03).
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForViewRetirementsAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanViewRetirementsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Manage gate for certificate requests (D-04): the dedicated <c>PersonnelFiles.ManageCertificateRequests</c>
    /// permission (or Admin). Used by process/issue/deliver/reject/update/delete — RR. HH. only, no self-service.
    /// Item-level optimistic concurrency is enforced by each command's own If-Match token, so callers pass
    /// <see cref="Guid.Empty"/> here.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageCertificateRequestsAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageCertificateRequestsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Create/cancel gate for certificate requests (D-02 self-service): the caller has the dedicated
    /// <c>PersonnelFiles.ManageCertificateRequests</c> permission (or Admin), OR the caller is the employee
    /// acting on their own file (self-service create / self-cancel of a pending request). Processing/issuance
    /// stay manager-only (see <see cref="LoadForManageCertificateRequestsAsync{TResponse}"/>).
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForCreateOwnOrManageCertificateAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canManageByRole = (await authorizationService.EnsureCanManageCertificateRequestsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canManageByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for certificate sub-resources (D-02): the caller has the dedicated
    /// <c>ViewCertificateRequests</c> permission (or Admin), OR the caller is the employee reading their own
    /// requests (self-service). Without the permission and without being the owner the caller gets 403. Requires
    /// a completed employee.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForCertificateReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canViewByRole = (await authorizationService.EnsureCanViewCertificateRequestsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canViewByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        return (null, personnelFile);
    }

    protected static void EnsureEmployeeRecordType(PersonnelFile personnelFile)
    {
        if (!personnelFile.IsCompletedEmployee)
        {
            throw new InvalidOperationException("Personnel file must be a completed employee record.");
        }
    }

    protected static void TouchPersonnelFile(PersonnelFile personnelFile)
    {
        personnelFile.UpdatePersonalInfo(
            personnelFile.RecordType,
            personnelFile.FirstName,
            personnelFile.LastName,
            personnelFile.BirthDate,
            personnelFile.MaritalStatus,
            personnelFile.Profession,
            personnelFile.Nationality,
            personnelFile.PersonalEmail,
            personnelFile.InstitutionalEmail,
            personnelFile.PersonalPhone,
            personnelFile.InstitutionalPhone,
            personnelFile.BirthCountry,
            personnelFile.BirthDepartment,
            personnelFile.BirthMunicipality,
            personnelFile.PhotoFilePublicId,
            personnelFile.OrgUnitPublicId,
            personnelFile.PersonalTitle,
            personnelFile.AfpCode);
    }

    /// <summary>
    /// Manage gate for off-payroll transactions (D-06): identical to <see cref="LoadForManageMedicalClaimsAsync{TResponse}"/>
    /// but enforces the dedicated <c>PersonnelFiles.ManageOffPayrollTransactions</c> permission. HR-only — there is
    /// NO self-service branch (the employee never writes these). Item-level optimistic concurrency is enforced by
    /// each command's own If-Match token, so callers pass <see cref="Guid.Empty"/> here.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageOffPayrollTransactionsAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageOffPayrollTransactionsAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Manage gate for position competencies (D-08): enforces the dedicated
    /// <c>PersonnelFiles.ManageCompetencies</c> permission (or Admin / IAM super-admin). Used by
    /// add/update/patch/delete — HR-only, no self-service write (CLARIHR is the source of truth, D-01).
    /// Item-level optimistic concurrency is enforced by each command's own If-Match token, so callers pass
    /// <see cref="Guid.Empty"/> here.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForManageCompetenciesAsync<TResponse>(
        Guid personnelFileId,
        Guid concurrencyToken,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageCompetenciesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<TResponse>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (concurrencyToken != Guid.Empty && personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }
}

internal abstract class PersonnelFileEmployeeReadQueryHandlerBase : PersonnelFileEmployeeCommandHandlerBase
{
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForReadAsync<TResponse>(
            personnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return (failure, null);
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for compensation sub-resources (D-16): the caller has the <c>ViewCompensation</c>
    /// permission (or Admin), OR the caller is the employee reading their own file (self-service).
    /// Unlike the standard read gate, this does NOT require <c>PersonnelFiles.Read</c>.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForCompensationReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canViewByRole = (await authorizationService.EnsureCanViewCompensationAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canViewByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for insurance sub-resources: the caller has the dedicated <c>ViewInsurance</c>
    /// permission (or Admin). Unlike the compensation read gate, there is NO self-service branch in
    /// this phase (D-11): the employee cannot read their own insurances.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForInsuranceReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (!(await authorizationService.EnsureCanViewInsuranceAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for medical-claim sub-resources (D-08 / D-09): the caller has the dedicated
    /// <c>ViewMedicalClaims</c> permission (or Admin), OR the caller is the employee reading their own claims
    /// (self-service). Diagnosis is health data: without the permission and without being the owner the caller
    /// gets 403 (no masking). Mirrors the compensation read gate.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForMedicalClaimReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canViewByRole = (await authorizationService.EnsureCanViewMedicalClaimsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canViewByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for incapacity sub-resources (vacaciones/incapacidades D-17/D-18): the caller has the dedicated
    /// <c>ViewIncapacities</c> permission (or Admin), OR the caller is the employee reading their own
    /// incapacities (self-service). Incapacities are health data: without the permission and without being the
    /// owner the caller gets 403 (no masking). Mirrors the medical-claim read gate. Requires a completed employee.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForIncapacityReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canViewByRole = (await authorizationService.EnsureCanViewIncapacitiesAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canViewByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for compensatory-time sub-resources (REQ-002 D-01/D-10): the caller has the dedicated
    /// <c>ViewCompensatoryTime</c> permission (or Admin), OR the caller is the employee reading their own fund /
    /// estado de cuenta (self-service). Without the permission and without being the owner the caller gets 403
    /// (no masking). Requires a completed employee.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForCompensatoryTimeReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canViewByRole = (await authorizationService.EnsureCanViewCompensatoryTimeAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canViewByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for off-payroll-transaction sub-resources (D-06): the caller has the dedicated
    /// <c>ViewOffPayrollTransactions</c> permission (or Admin). Like the insurance read gate, there is NO
    /// self-service branch — these are internal HR records (sensitive amounts), so the employee cannot read them.
    /// Requires a completed employee.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForOffPayrollReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (!(await authorizationService.EnsureCanViewOffPayrollTransactionsAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

    /// <summary>
    /// Read gate for position competencies ("Competencias del puesto", D-08/D-09): the caller has the dedicated
    /// <c>PersonnelFiles.ViewCompetencies</c> permission (or Admin / IAM super-admin), OR the caller is the
    /// employee reading their own file (self-service). Requires a completed employee.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadCompletedEmployeeForCompetencyReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        var canViewByRole = (await authorizationService.EnsureCanViewCompetenciesAsync(tenantContext.TenantId.Value, cancellationToken)).IsSuccess;
        if (!canViewByRole)
        {
            var isSelf = personnelFile.LinkedUserPublicId is { } linkedUserPublicId
                && Guid.TryParse(currentUserService.UserId, out var callerUserPublicId)
                && linkedUserPublicId == callerUserPublicId;
            if (!isSelf)
            {
                return (Result<TResponse>.Failure(PersonnelFileErrors.Forbidden), null);
            }
        }

        if (!personnelFile.IsCompletedEmployee)
        {
            return (Result<TResponse>.Failure(PersonnelFileErrors.StateRuleViolation), null);
        }

        return (null, personnelFile);
    }

}

