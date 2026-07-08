using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Resolved authorization document (D-20): the internal document-type id and the stored file to snapshot.</summary>
internal sealed record CompensatoryTimeDocumentResolved(long? DocumentTypeInternalId, StoredFile StoredFile);

/// <summary>
/// Shared write glue of the compensatory-time credit handlers: resolves the mandatory-by-preference authorization
/// document (patrón nuevo — the gate of purpose lives in the credit CREATE, aclaración №4) and computes the
/// credited hours (worked × factor snapshot, or a manual override with a mandatory note — RN-02).
/// </summary>
internal static class CompensatoryTimeCreditWriteSupport
{
    /// <summary>
    /// Validates the authorization-document rule (D-20): when the preference requires a document the file id is
    /// mandatory; when a file id is supplied it must be an active tenant file uploaded with the
    /// compensatory-time-document purpose, and any supplied document type must be an active catalog item.
    /// </summary>
    public static async Task<Result<CompensatoryTimeDocumentResolved?>> ResolveDocumentAsync(
        Guid? authorizationFilePublicId,
        Guid? documentTypeCatalogItemPublicId,
        string? _,
        bool requiresDocument,
        Guid tenantId,
        IFileRepository fileRepository,
        IDocumentTypeCatalogRepository documentTypeCatalogRepository,
        CancellationToken cancellationToken)
    {
        if (authorizationFilePublicId is not { } filePublicId)
        {
            return requiresDocument
                ? Result<CompensatoryTimeDocumentResolved?>.Failure(CompensatoryTimeCreditErrors.DocumentRequired)
                : Result<CompensatoryTimeDocumentResolved?>.Success(null);
        }

        var storedFile = await fileRepository.GetByPublicIdAsync(filePublicId, cancellationToken);
        if (storedFile is null)
        {
            return Result<CompensatoryTimeDocumentResolved?>.Failure(FileErrors.FileNotFound);
        }

        if (storedFile.Status != FileStatus.Active)
        {
            return Result<CompensatoryTimeDocumentResolved?>.Failure(FileErrors.FileNotActive);
        }

        if (storedFile.TenantId != tenantId)
        {
            return Result<CompensatoryTimeDocumentResolved?>.Failure(FileErrors.FileTenantMismatch);
        }

        if (storedFile.Purpose != FilePurpose.CompensatoryTimeDocument)
        {
            return Result<CompensatoryTimeDocumentResolved?>.Failure(CompensatoryTimeCreditErrors.DocumentPurposeInvalid);
        }

        long? documentTypeInternalId = null;
        if (documentTypeCatalogItemPublicId is { } documentTypePublicId)
        {
            var lookup = await documentTypeCatalogRepository.GetActiveLookupByIdAsync(documentTypePublicId, cancellationToken);
            if (lookup is null)
            {
                return Result<CompensatoryTimeDocumentResolved?>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]>
                    {
                        ["documentTypeCatalogItemPublicId"] = ["The specified document type does not exist or is inactive."]
                    }));
            }

            documentTypeInternalId = lookup.InternalId;
        }

        return Result<CompensatoryTimeDocumentResolved?>.Success(
            new CompensatoryTimeDocumentResolved(documentTypeInternalId, storedFile));
    }

    /// <summary>
    /// Resolves the credited hours (RN-02): a manual override (with a mandatory note) wins over the computed
    /// <c>Round2(worked × factor)</c>. Returns the error when the override note is missing.
    /// </summary>
    public static Result<CreditedHoursResult> ResolveCreditedHours(CompensatoryTimeCreditInput item, decimal factor)
    {
        if (item.HoursCreditedOverride is { } overrideHours)
        {
            if (string.IsNullOrWhiteSpace(item.OverrideNote))
            {
                return Result<CreditedHoursResult>.Failure(CompensatoryTimeCreditErrors.OverrideNoteRequired);
            }

            return Result<CreditedHoursResult>.Success(
                new CreditedHoursResult(CompensatoryTimeRules.Round2(overrideHours), true, item.OverrideNote));
        }

        return Result<CreditedHoursResult>.Success(
            new CreditedHoursResult(CompensatoryTimeRules.CreditedHours(item.HoursWorked, factor), false, null));
    }

    public readonly record struct CreditedHoursResult(decimal HoursCredited, bool IsOverridden, string? OverrideNote);

    /// <summary>Journals the ACREDITACION_TIEMPO_COMPENSATORIO personnel action in the same transaction (fila #13).</summary>
    public static async Task AddCreditJournalAsync(
        IPersonnelFileEmployeeRepository employeeRepository,
        long personnelFileId,
        Guid tenantId,
        PersonnelFileCompensatoryTimeCredit entity,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var action = PersonnelFilePersonnelAction.Create(
            "ACREDITACION_TIEMPO_COMPENSATORIO",
            "APLICADA",
            actionDateUtc: nowUtc,
            effectiveFromUtc: entity.WorkDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            effectiveToUtc: null,
            description: $"ACREDITACION_TIEMPO_COMPENSATORIO ({entity.HoursCredited:0.##} h) {entity.WorkDate:yyyy-MM-dd}.",
            reference: null,
            amount: null,
            currencyCode: null,
            isSystemGenerated: true);
        action.BindToPersonnelFile(personnelFileId);
        action.SetTenantId(tenantId);
        _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);
    }

    /// <summary>The worked-date ≤ today (RN-15) and time-range coherence pre-checks (clean 422 before the guard).</summary>
    public static Error? ValidateDeclarative(CompensatoryTimeCreditInput item, DateOnly today)
    {
        if (item.WorkDate > today)
        {
            return CompensatoryTimeCreditErrors.WorkDateInFuture;
        }

        if (item.StartTime is { } start && item.EndTime is { } end && end <= start)
        {
            return CompensatoryTimeCreditErrors.TimeRangeInvalid;
        }

        return null;
    }
}

internal sealed class AddCompensatoryTimeCreditCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IFileRepository fileRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddCompensatoryTimeCreditCommand, PersonnelFileCompensatoryTimeCreditResponse>
{
    public async Task<Result<PersonnelFileCompensatoryTimeCreditResponse>> Handle(
        AddCompensatoryTimeCreditCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompensatoryTimeAsync<PersonnelFileCompensatoryTimeCreditResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;
        if (await compensatoryTimeRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var item = command.Item;
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        if (CompensatoryTimeCreditWriteSupport.ValidateDeclarative(item, today) is { } declarativeError)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(declarativeError);
        }

        var type = await compensatoryTimeRepository.ResolveTypeAsync(tenantId, item.CompensatoryTimeTypePublicId, cancellationToken);
        if (type is null)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(CompensatoryTimeCreditErrors.TypeInvalid);
        }

        if (type.OperationCode == CompensatoryTimeOperations.Debits)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(CompensatoryTimeCreditErrors.TypeOperationMismatch);
        }

        var creditedResult = CompensatoryTimeCreditWriteSupport.ResolveCreditedHours(item, type.CreditFactor);
        if (creditedResult.IsFailure)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(creditedResult.Error);
        }

        var credited = creditedResult.Value;

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var requiresDocument = preference?.CompensatoryTimeCreditRequiresDocument ?? true;
        var maxBalance = preference?.CompensatoryTimeMaxBalanceHours;

        var documentResult = await CompensatoryTimeCreditWriteSupport.ResolveDocumentAsync(
            command.AuthorizationFilePublicId, command.DocumentTypeCatalogItemPublicId, command.DocumentObservations,
            requiresDocument, tenantId, fileRepository, documentTypeCatalogRepository, cancellationToken);
        if (documentResult.IsFailure)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(documentResult.Error);
        }

        var authorizerFilePublicId = documentResult.Value?.StoredFile.PublicId;
        var nowUtc = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // A credit only raises the balance, but the fund cap is a cross-row invariant, so serialize under the
            // advisory lock and re-read the balance inside the transaction (RN-11, aclaración №3).
            await compensatoryTimeRepository.AcquireFundLockAsync(tenantId, personnelFile.Id, cancellationToken);
            var currentBalance = await compensatoryTimeRepository.GetBalanceAsync(personnelFile.Id, cancellationToken);
            if (maxBalance is { } cap && currentBalance + credited.HoursCredited > cap)
            {
                return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(CompensatoryTimeCreditErrors.MaxBalanceExceeded);
            }

            var entity = PersonnelFileCompensatoryTimeCredit.Create(
                type.InternalId,
                type.Name,
                item.WorkDate,
                item.StartTime,
                item.EndTime,
                item.HoursWorked,
                item.WorkDetail,
                item.AuthorizedByText,
                authorizerFilePublicId,
                item.AssignedPositionPublicId,
                item.OvertimeRecordPublicId,
                currentUserService.UserId ?? string.Empty,
                item.Notes);
            entity.ApplyCreditedHours(type.CreditFactor, credited.HoursCredited, credited.IsOverridden, credited.OverrideNote);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(tenantId);
            compensatoryTimeRepository.AddCredit(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            if (documentResult.Value is { } resolvedDocument)
            {
                var document = PersonnelFileCompensatoryTimeCreditDocument.Create(
                    Guid.NewGuid(),
                    resolvedDocument.DocumentTypeInternalId,
                    resolvedDocument.StoredFile.PublicId,
                    resolvedDocument.StoredFile.FileName,
                    resolvedDocument.StoredFile.ContentType,
                    (int)resolvedDocument.StoredFile.SizeBytes,
                    command.DocumentObservations);
                document.BindToCredit(entity.Id);
                document.SetTenantId(tenantId);
                compensatoryTimeRepository.AddDocument(document);
            }

            await CompensatoryTimeCreditWriteSupport.AddCreditJournalAsync(
                employeeRepository, personnelFile.Id, tenantId, entity, nowUtc, cancellationToken);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await compensatoryTimeRepository.GetCreditResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time credit response could not be resolved after creation.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Registered a compensatory-time credit for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCompensatoryTimeCreditCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdateCompensatoryTimeCreditCommand, PersonnelFileCompensatoryTimeCreditResponse>
{
    public async Task<Result<PersonnelFileCompensatoryTimeCreditResponse>> Handle(
        UpdateCompensatoryTimeCreditCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompensatoryTimeAsync<PersonnelFileCompensatoryTimeCreditResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var tenantId = personnelFile!.TenantId;
        if (await compensatoryTimeRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var entity = await compensatoryTimeRepository.GetCreditEntityAsync(personnelFile.PublicId, command.CompensatoryTimeCreditPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.StatusCode != CompensatoryTimeStatuses.Registrada)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(CompensatoryTimeCreditErrors.StateRuleViolation);
        }

        var item = command.Item;
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        if (CompensatoryTimeCreditWriteSupport.ValidateDeclarative(item, today) is { } declarativeError)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(declarativeError);
        }

        var type = await compensatoryTimeRepository.ResolveTypeAsync(tenantId, item.CompensatoryTimeTypePublicId, cancellationToken);
        if (type is null)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(CompensatoryTimeCreditErrors.TypeInvalid);
        }

        if (type.OperationCode == CompensatoryTimeOperations.Debits)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(CompensatoryTimeCreditErrors.TypeOperationMismatch);
        }

        var creditedResult = CompensatoryTimeCreditWriteSupport.ResolveCreditedHours(item, type.CreditFactor);
        if (creditedResult.IsFailure)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(creditedResult.Error);
        }

        var credited = creditedResult.Value;
        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var maxBalance = preference?.CompensatoryTimeMaxBalanceHours;
        var previousHoursCredited = entity.HoursCredited;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // R-T1: editing the hours changes the balance both ways, so serialize under the advisory lock and
            // re-verify the never-negative and cap invariants against the balance excluding this credit's old value.
            await compensatoryTimeRepository.AcquireFundLockAsync(tenantId, personnelFile.Id, cancellationToken);
            var balanceExcludingThis = await compensatoryTimeRepository.GetBalanceAsync(personnelFile.Id, cancellationToken) - previousHoursCredited;
            var newBalance = balanceExcludingThis + credited.HoursCredited;
            if (newBalance < 0m)
            {
                return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(CompensatoryTimeCreditErrors.BalanceWouldGoNegative);
            }

            if (maxBalance is { } cap && newBalance > cap)
            {
                return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(CompensatoryTimeCreditErrors.MaxBalanceExceeded);
            }

            entity.Update(
                type.InternalId,
                type.Name,
                item.WorkDate,
                item.StartTime,
                item.EndTime,
                item.HoursWorked,
                item.WorkDetail,
                item.AuthorizedByText,
                entity.AuthorizerFilePublicId,
                item.AssignedPositionPublicId,
                item.OvertimeRecordPublicId,
                item.Notes);
            entity.ApplyCreditedHours(type.CreditFactor, credited.HoursCredited, credited.IsOverridden, credited.OverrideNote);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await compensatoryTimeRepository.GetCreditResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time credit response could not be resolved after update.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Updated a compensatory-time credit for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AnnulCompensatoryTimeCreditCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulCompensatoryTimeCreditCommand, PersonnelFileCompensatoryTimeCreditResponse>
{
    public async Task<Result<PersonnelFileCompensatoryTimeCreditResponse>> Handle(
        AnnulCompensatoryTimeCreditCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompensatoryTimeAsync<PersonnelFileCompensatoryTimeCreditResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var tenantId = personnelFile!.TenantId;
        if (await compensatoryTimeRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var entity = await compensatoryTimeRepository.GetCreditEntityAsync(personnelFile.PublicId, command.CompensatoryTimeCreditPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.StatusCode != CompensatoryTimeStatuses.Registrada)
        {
            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(CompensatoryTimeCreditErrors.StateRuleViolation);
        }

        var creditHours = entity.HoursCredited;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // R-T1: annulling a credit lowers the balance; if debits already stand against it the balance would go
            // negative — re-verify under the advisory lock inside the transaction (RN-06, aclaración №3).
            await compensatoryTimeRepository.AcquireFundLockAsync(tenantId, personnelFile.Id, cancellationToken);
            var balance = await compensatoryTimeRepository.GetBalanceAsync(personnelFile.Id, cancellationToken);
            if (balance - creditHours < 0m)
            {
                return Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(CompensatoryTimeCreditErrors.BalanceWouldGoNegative);
            }

            entity.Annul(command.Reason, currentUserService.UserId ?? string.Empty, dateTimeProvider.UtcNow);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await compensatoryTimeRepository.GetCreditResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time credit response could not be resolved after annulment.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Annulled a compensatory-time credit for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileCompensatoryTimeCreditResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetCompensatoryTimeCreditsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCompensatoryTimeCreditsQuery, IReadOnlyCollection<PersonnelFileCompensatoryTimeCreditResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileCompensatoryTimeCreditResponse>>> Handle(
        GetCompensatoryTimeCreditsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensatoryTimeReadAsync<IReadOnlyCollection<PersonnelFileCompensatoryTimeCreditResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await compensatoryTimeRepository.GetCreditResponsesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileCompensatoryTimeCreditResponse>>.Success(response);
    }
}

internal sealed class GetCompensatoryTimeCreditByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCompensatoryTimeCreditByIdQuery, PersonnelFileCompensatoryTimeCreditResponse>
{
    public async Task<Result<PersonnelFileCompensatoryTimeCreditResponse>> Handle(
        GetCompensatoryTimeCreditByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensatoryTimeReadAsync<PersonnelFileCompensatoryTimeCreditResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await compensatoryTimeRepository.GetCreditResponseAsync(personnelFile!.PublicId, query.CompensatoryTimeCreditPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileCompensatoryTimeCreditResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileCompensatoryTimeCreditResponse>.Success(response);
    }
}
