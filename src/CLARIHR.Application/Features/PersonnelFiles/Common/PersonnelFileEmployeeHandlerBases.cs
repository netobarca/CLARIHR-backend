using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
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
            personnelFile.AssignedPositionSlotPublicId);
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
}

