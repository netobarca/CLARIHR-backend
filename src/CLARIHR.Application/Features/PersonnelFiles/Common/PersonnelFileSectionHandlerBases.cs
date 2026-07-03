using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using FluentValidation;
using FluentValidation.Results;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Returned by sub-resource DELETE endpoints so the caller receives the parent
/// personnel file's refreshed concurrency token without an extra round-trip,
/// mirroring the JobProfile sub-resource canonical pattern.
/// </summary>
public sealed record PersonnelFileParentConcurrencyResult(Guid ParentConcurrencyToken);

public sealed record PersonnelFileSectionResult<TData>(
    TData Data,
    Guid PersonnelFileConcurrencyToken,
    DateTime? ModifiedAtUtc);

public sealed record PersonnelFileSectionResult(
    Guid PersonnelFileConcurrencyToken,
    DateTime? ModifiedAtUtc);

public enum PersonnelFileTrackedSection
{
    Identifications = 1,
    Addresses = 2,
    EmergencyContacts = 3,
    FamilyMembers = 4,
    Hobbies = 5,
    EmployeeRelations = 6,
    BankAccounts = 7,
    Associations = 8,
    Educations = 9,
    Languages = 10,
    Trainings = 11,
    PreviousEmployments = 12,
    References = 13,
    Documents = 14
}

internal abstract class GetPersonnelFileSectionQueryHandlerBase
{
    protected static async Task<Result<TResponse>?> EnsureCanReadAsync<TResponse>(
        Guid personnelFileId,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository repository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<TResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var authorizationResult = await authorizationService.EnsureCanReadAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<TResponse>.Failure(authorizationResult.Error);
        }

        var personnelFile = await repository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is not null)
        {
            return null;
        }

        return Result<TResponse>.Failure(
            await repository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                ? authorizationService.TenantMismatch(RbacPermissionAction.Read)
                : PersonnelFileErrors.NotFound);
    }
}

internal abstract class PersonnelFileSectionCommandHandlerBase
{
    /// <summary>
    /// Shared write prologue for per-item personnel-file section mutations (add / update /
    /// delete / patch): tenant presence → <c>EnsureCanManageAsync</c> → load the targeted
    /// section for update → not-found / cross-tenant mapping. Optimistic concurrency for these
    /// endpoints is enforced at the <b>item</b> level by each handler (the item's own
    /// If-Match token), so this prologue performs no parent-file concurrency check — mirroring
    /// the employee <c>LoadForManageAsync</c>. Returns the loaded file on success; otherwise a
    /// failure <see cref="Result{TResponse}"/> and a null file. Replaces ~56 hand-inlined copies
    /// of this gate so the tenant/authz/not-found behavior has a single source of truth.
    /// </summary>
    protected static async Task<(Result<TResponse>? Failure, PersonnelFile? File)> LoadForSectionManageAsync<TResponse>(
        Guid personnelFileId,
        PersonnelFileTrackedSection section,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository repository,
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

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(personnelFileId, section, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<TResponse>.Failure(
                    await repository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        return (null, personnelFile);
    }
}

internal abstract class ReplacePersonnelFileSectionCommandHandlerBase
{
    internal static PersonnelFileSectionResult<TSection> CreateSectionResult<TSection>(
        PersonnelFile personnelFile,
        TSection data) =>
        new(data, personnelFile.ConcurrencyToken, personnelFile.ModifiedUtc);

    /// <summary>
    /// Shared personal-info mutation flow used by both the PUT
    /// (<see cref="UpdatePersonnelFileCommandHandler"/>) and the PATCH
    /// (<see cref="PatchPersonnelFileCommandHandler"/>) handlers, so the catalog-code
    /// validation + profile-photo write plan + transactional <c>UpdatePersonalInfo</c> +
    /// audit + persistence live in one place. The caller has already loaded the entity and
    /// checked tenant / concurrency / record-type. <paramref name="desiredIsActive"/> drives
    /// the optional lifecycle toggle (PUT passes the current value = no-op; PATCH passes the
    /// patched value). <paramref name="auditFactory"/> is invoked <b>after</b> the mutation so
    /// each caller emits its own audit descriptor against the post-mutation entity (PUT logs
    /// <c>PersonnelFileUpdated</c>; PATCH logs the lifecycle-transition event).
    /// </summary>
    internal static async Task<Result<PersonnelFilePersonalInfoResponse>> ApplyPersonalInfoAsync(
        PersonnelFile personnelFile,
        UpdatePersonnelFileCommand values,
        bool desiredIsActive,
        Func<PersonnelFile, (string EventType, string Action, string Summary)> auditFactory,
        IPersonnelFileRepository repository,
        IPersonnelFileProfilePhotoService profilePhotoService,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var personalInfoCatalogValidation = await PersonnelReferenceCatalogValidation.ValidatePersonalInfoCodesAsync(
            repository,
            personnelFile.TenantId,
            values.MaritalStatusCode,
            values.ProfessionCode,
            values.BirthCountryCode,
            values.BirthDepartmentCode,
            values.BirthMunicipalityCode,
            cancellationToken,
            values.PersonalTitleCode);
        if (personalInfoCatalogValidation != Error.None)
        {
            return Result<PersonnelFilePersonalInfoResponse>.Failure(personalInfoCatalogValidation);
        }

        // AFP is a General-family catalog (RF-007): validated separately from the Reference codes.
        if (!string.IsNullOrWhiteSpace(values.AfpCode))
        {
            var afpValidation = await PersonnelCurriculumCatalogValidation.ValidateCodeAsync(
                repository,
                personnelFile.TenantId,
                "afpCode",
                PersonnelCurriculumCatalogCategories.Afp,
                values.AfpCode,
                cancellationToken);
            if (afpValidation != Error.None)
            {
                return Result<PersonnelFilePersonalInfoResponse>.Failure(afpValidation);
            }
        }

        var photoWritePlanResult = await profilePhotoService.PrepareWriteAsync(
            personnelFile.TenantId,
            personnelFile.PublicId,
            values.PhotoFilePublicId,
            personnelFile.PhotoFilePublicId,
            cancellationToken);
        if (photoWritePlanResult.IsFailure)
        {
            return Result<PersonnelFilePersonalInfoResponse>.Failure(photoWritePlanResult.Error);
        }

        var photoWritePlan = photoWritePlanResult.Value;

        var before = await repository.GetPersonalInfoAsync(personnelFile.PublicId, cancellationToken)
            ?? throw new InvalidOperationException("Personnel file personal info could not be resolved before the update.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            try
            {
                personnelFile.UpdatePersonalInfo(
                    values.RecordType,
                    values.FirstName,
                    values.LastName,
                    values.BirthDate,
                    values.MaritalStatusCode,
                    values.ProfessionCode,
                    values.Nationality,
                    values.PersonalEmail,
                    values.InstitutionalEmail,
                    values.PersonalPhone,
                    values.InstitutionalPhone,
                    values.BirthCountryCode,
                    values.BirthDepartmentCode,
                    values.BirthMunicipalityCode,
                    photoWritePlan.PersistedPhotoFilePublicId,
                    values.OrgUnitId,
                    values.PersonalTitleCode,
                    values.AfpCode);
            }
            catch (InvalidOperationException)
            {
                await profilePhotoService.CleanupAfterPersistenceFailureAsync(photoWritePlan, cancellationToken);
                return Result<PersonnelFilePersonalInfoResponse>.Failure(PersonnelFileErrors.ProvisioningFieldsLocked);
            }

            if (desiredIsActive != personnelFile.IsActive)
            {
                if (desiredIsActive)
                {
                    personnelFile.Activate();
                }
                else
                {
                    personnelFile.Inactivate();
                }
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await repository.GetPersonalInfoAsync(personnelFile.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Personnel file personal info could not be resolved after the update.");

            var (eventType, action, summary) = auditFactory(personnelFile);
            await auditService.LogAsync(
                new AuditLogEntry(
                    eventType,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    action,
                    summary,
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PersonalInfo,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = PersonnelFilePrintSections.PersonalInfo,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceSuccessAsync(photoWritePlan, cancellationToken);

            return Result<PersonnelFilePersonalInfoResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await profilePhotoService.CleanupAfterPersistenceFailureAsync(photoWritePlan, cancellationToken);
            throw;
        }
    }

    protected static async Task<(Result<PersonnelFileSectionResult<TSection>>? Failure, PersonnelFile? File)> LoadForUpdateAsync<TSection>(
        Guid personnelFileId,
        Guid concurrencyToken,
        PersonnelFileTrackedSection trackedSection,
        ITenantContext tenantContext,
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository repository,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<PersonnelFileSectionResult<TSection>>.Failure(AuthorizationErrors.Unauthenticated), null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<PersonnelFileSectionResult<TSection>>.Failure(authorizationResult.Error), null);
        }

        var personnelFile = await repository.GetForProfileSectionUpdateAsync(personnelFileId, trackedSection, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<PersonnelFileSectionResult<TSection>>.Failure(
                    await repository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null);
        }

        if (personnelFile.ConcurrencyToken != concurrencyToken)
        {
            return (Result<PersonnelFileSectionResult<TSection>>.Failure(PersonnelFileErrors.ConcurrencyConflict), null);
        }

        return (null, personnelFile);
    }

    protected static async Task<Result<PersonnelFileSectionResult<TSection>>> PersistSectionAsync<TSection>(
        PersonnelFile personnelFile,
        string sectionName,
        string auditMessage,
        Func<Guid, CancellationToken, Task<TSection>> sectionLoader,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        string eventType,
        CancellationToken cancellationToken)
    {
        var before = await sectionLoader(personnelFile.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await sectionLoader(personnelFile.PublicId, cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    eventType,
                    AuditEntityTypes.PersonnelFile,
                    personnelFile.PublicId,
                    personnelFile.FullName,
                    AuditActions.Update,
                    auditMessage,
                    Before: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = sectionName,
                        data = before
                    },
                    After: new
                    {
                        personnelFileId = personnelFile.PublicId,
                        section = sectionName,
                        data = after,
                        personnelFileConcurrencyToken = personnelFile.ConcurrencyToken,
                        modifiedAtUtc = personnelFile.ModifiedUtc
                    }),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PersonnelFileSectionResult<TSection>>.Success(CreateSectionResult(personnelFile, after));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

