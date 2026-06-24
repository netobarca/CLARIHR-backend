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
            personnelFile.OrgUnitPublicId);
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
}

