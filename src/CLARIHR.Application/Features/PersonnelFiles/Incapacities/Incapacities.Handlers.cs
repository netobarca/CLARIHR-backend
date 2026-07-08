using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.DocumentTypeCatalogs;
using CLARIHR.Application.Abstractions.Files;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Files;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Shared calculation glue of the incapacity handlers: turns a resolved <see cref="LeaveCalculationContext"/>
/// plus the dates and the (freshly re-read) employer-cap remaining into the entity snapshot and the wire
/// warnings — the ONE place the pure engine is invoked from the write handlers (§3.5, R-T2).
/// </summary>
internal static class IncapacityCalculationSupport
{
    public readonly record struct CalculationResult(
        IncapacityCalculationSnapshot Snapshot,
        IReadOnlyList<IncapacityCalculationWarningResponse> Warnings);

    public static CalculationResult Run(
        LeaveCalculationContext context,
        DateOnly startDate,
        DateOnly? endDate,
        decimal employerCapRemaining)
    {
        var tranches = context.Risk.Tranches
            .Select(tranche => new IncapacityTrancheParameter(
                tranche.DayFrom, tranche.DayTo, tranche.SubsidyPercent, tranche.PayerCode))
            .ToList();

        var input = new IncapacityCalculationInput(
            startDate,
            endDate,
            context.Risk.CountsSeventhDay,
            context.Risk.CountsSaturday,
            context.Risk.CountsHoliday,
            context.Risk.HasSubsidy,
            tranches,
            context.Holidays,
            context.RestDay,
            context.ChainOffsetDays,
            context.MonthlyBaseSalary ?? 0m,
            employerCapRemaining);

        var result = IncapacityCalculationRules.Calculate(input);

        var snapshot = new IncapacityCalculationSnapshot(
            result.CalendarDays,
            result.ComputableDays,
            result.SubsidizedDays,
            result.DiscountDays,
            result.EmployerDays,
            result.MonthlyBaseSalary,
            result.DailySalary,
            result.SubsidyAmount,
            result.DiscountAmount,
            result.EmployerAmount,
            result.TrancheDetails.Count > 0 ? IncapacityCalculationRules.SerializeTrancheDetail(result) : null);

        var warnings = result.Warnings
            .Select(warning => new IncapacityCalculationWarningResponse(warning.Code, warning.Parameters))
            .ToArray();

        return new CalculationResult(snapshot, warnings);
    }

    /// <summary>Employer-cap total for the year (D-27): covered + benefit, legal defaults 9 / 0 when unset.</summary>
    public static int ResolveEmployerCapTotal(int? covered, int? benefit) =>
        (covered ?? IncapacityBalanceRules.DefaultEmployerCoveredDaysPerYear)
        + (benefit ?? IncapacityBalanceRules.DefaultAdditionalBenefitDaysPerYear);
}

/// <summary>Resolved constancia (D-22): the internal document-type id and the stored file to snapshot.</summary>
internal sealed record IncapacityDocumentResolved(long? DocumentTypeInternalId, Domain.Files.StoredFile StoredFile);

internal static class IncapacityWriteSupport
{
    /// <summary>
    /// Validates the constancia rule (D-22): when the preference requires a document the file id is mandatory;
    /// when a file id is supplied it must be an active tenant file uploaded with the incapacity-document purpose,
    /// and any supplied document type must be an active catalog item.
    /// </summary>
    public static async Task<Result<IncapacityDocumentResolved?>> ResolveDocumentAsync(
        Guid? documentFilePublicId,
        Guid? documentTypeCatalogItemPublicId,
        bool requiresDocument,
        Guid tenantId,
        IFileRepository fileRepository,
        IDocumentTypeCatalogRepository documentTypeCatalogRepository,
        CancellationToken cancellationToken)
    {
        if (documentFilePublicId is not { } filePublicId)
        {
            return requiresDocument
                ? Result<IncapacityDocumentResolved?>.Failure(IncapacityErrors.DocumentRequired)
                : Result<IncapacityDocumentResolved?>.Success(null);
        }

        var storedFile = await fileRepository.GetByPublicIdAsync(filePublicId, cancellationToken);
        if (storedFile is null)
        {
            return Result<IncapacityDocumentResolved?>.Failure(FileErrors.FileNotFound);
        }

        if (storedFile.Status != FileStatus.Active)
        {
            return Result<IncapacityDocumentResolved?>.Failure(FileErrors.FileNotActive);
        }

        if (storedFile.TenantId != tenantId)
        {
            return Result<IncapacityDocumentResolved?>.Failure(FileErrors.FileTenantMismatch);
        }

        if (storedFile.Purpose != FilePurpose.IncapacityDocument)
        {
            return Result<IncapacityDocumentResolved?>.Failure(IncapacityErrors.DocumentPurposeInvalid);
        }

        long? documentTypeInternalId = null;
        if (documentTypeCatalogItemPublicId is { } documentTypePublicId)
        {
            var lookup = await documentTypeCatalogRepository.GetActiveLookupByIdAsync(documentTypePublicId, cancellationToken);
            if (lookup is null)
            {
                return Result<IncapacityDocumentResolved?>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]>
                    {
                        ["documentTypeCatalogItemPublicId"] = ["The specified document type does not exist or is inactive."]
                    }));
            }

            documentTypeInternalId = lookup.InternalId;
        }

        return Result<IncapacityDocumentResolved?>.Success(new IncapacityDocumentResolved(documentTypeInternalId, storedFile));
    }
}

internal sealed class AddPersonnelFileIncapacityCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ILeaveCalculationDataProvider dataProvider,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IFileRepository fileRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileIncapacityCommand, PersonnelFileIncapacityResponse>
{
    public async Task<Result<PersonnelFileIncapacityResponse>> Handle(
        AddPersonnelFileIncapacityCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, isManager) = await LoadForCreateOwnOrManageIncapacityAsync<PersonnelFileIncapacityResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var item = command.Item;
        var tenantId = personnelFile.TenantId;

        var riskInternalId = await incapacityRepository.ResolveRiskInternalIdAsync(tenantId, item.RiskPublicId, cancellationToken);
        if (riskInternalId is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.RiskInvalid);
        }

        var typeInternalId = await incapacityRepository.ResolveIncapacityTypeInternalIdAsync(tenantId, item.IncapacityTypePublicId, cancellationToken);
        if (typeInternalId is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.TypeInvalid);
        }

        long? clinicInternalId = null;
        if (item.MedicalClinicPublicId is { } clinicPublicId)
        {
            clinicInternalId = await incapacityRepository.ResolveMedicalClinicInternalIdAsync(tenantId, clinicPublicId, cancellationToken);
            if (clinicInternalId is null)
            {
                return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.ClinicInvalid);
            }
        }

        long? payrollPeriodInternalId = null;
        if (item.PayrollPeriodDefinitionPublicId is { } payrollPeriodPublicId)
        {
            payrollPeriodInternalId = await incapacityRepository.ResolvePayrollPeriodInternalIdAsync(tenantId, payrollPeriodPublicId, cancellationToken);
            if (payrollPeriodInternalId is null)
            {
                return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.PayrollPeriodInvalid);
            }
        }

        var context = await dataProvider.GetCalculationContextAsync(
            tenantId, personnelFile.Id, item.AssignedPositionPublicId, riskInternalId.Value,
            item.StartDate, item.EndDate, excludeIncapacityId: null, extendsIncapacityId: null, cancellationToken);
        if (context is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.RiskInvalid);
        }

        if (item.EndDate is null && !context.Risk.AllowsIndefinite)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.EndDateRequired);
        }

        if (context.MonthlyBaseSalary is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.BaseSalaryMissing);
        }

        if (await incapacityRepository.HasOverlappingIncapacityAsync(
                personnelFile.Id, item.StartDate, item.EndDate, excludeIncapacityId: null, cancellationToken))
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.Overlap);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var requiresDocument = preference?.IncapacityRequiresDocument ?? true;
        var documentResult = await IncapacityWriteSupport.ResolveDocumentAsync(
            item.DocumentFilePublicId, item.DocumentTypeCatalogItemPublicId, requiresDocument, tenantId,
            fileRepository, documentTypeCatalogRepository, cancellationToken);
        if (documentResult.IsFailure)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(documentResult.Error);
        }

        var origin = isManager ? IncapacityOrigins.Rrhh : IncapacityOrigins.Autoservicio;
        var entity = PersonnelFileIncapacity.Create(
            requesterFilePublicId: isManager ? null : personnelFile.PublicId,
            requesterNameSnapshot: isManager ? null : personnelFile.FullName,
            requestedByUserId: currentUserService.UserId ?? string.Empty,
            originCode: origin,
            incapacityRiskId: riskInternalId.Value,
            riskCodeSnapshot: context.Risk.Code,
            riskCountsSeventhDaySnapshot: context.Risk.CountsSeventhDay,
            riskCountsSaturdaySnapshot: context.Risk.CountsSaturday,
            riskCountsHolidaySnapshot: context.Risk.CountsHoliday,
            riskUsesFundSnapshot: context.Risk.UsesFund,
            riskHasSubsidySnapshot: context.Risk.HasSubsidy,
            medicalClinicId: clinicInternalId,
            incapacityTypeId: typeInternalId.Value,
            assignedPositionPublicId: item.AssignedPositionPublicId,
            payrollTypeCode: item.PayrollTypeCode,
            payrollPeriodDefinitionId: payrollPeriodInternalId,
            startDate: item.StartDate,
            endDate: item.EndDate,
            extendsIncapacityId: null,
            notes: item.Notes,
            riskAllowsIndefinite: context.Risk.AllowsIndefinite);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(tenantId);

        var capTotal = IncapacityCalculationSupport.ResolveEmployerCapTotal(
            preference?.EmployerCoveredIncapacityDaysPerYear, preference?.AdditionalIncapacityBenefitDaysPerYear);
        var nowUtc = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // R-T2: re-read the year's consumption INSIDE the transaction so a concurrent REGISTRADA cannot
            // let this record over-draw the employer cap.
            var consumed = await incapacityRepository.GetRegisteredEmployerDaysConsumedAsync(
                personnelFile.Id, item.StartDate.Year, excludeIncapacityId: null, cancellationToken);
            var capRemaining = Math.Max(0, capTotal - consumed);

            var calculation = IncapacityCalculationSupport.Run(context, item.StartDate, item.EndDate, capRemaining);
            entity.ApplyCalculation(calculation.Snapshot);
            incapacityRepository.Add(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            if (documentResult.Value is { } resolvedDocument)
            {
                var document = PersonnelFileIncapacityDocument.Create(
                    Guid.NewGuid(),
                    resolvedDocument.DocumentTypeInternalId,
                    resolvedDocument.StoredFile.PublicId,
                    resolvedDocument.StoredFile.FileName,
                    resolvedDocument.StoredFile.ContentType,
                    (int)resolvedDocument.StoredFile.SizeBytes,
                    item.DocumentObservations);
                document.BindToIncapacity(entity.Id);
                document.SetTenantId(tenantId);
                incapacityRepository.AddDocument(document);
            }

            // Journal INCAPACIDAD only when the record is REGISTRADA (RRHH create); an EN_REVISION
            // self-registration journals at HR confirmation instead (D-15/D-18).
            if (IncapacityStatuses.CountsAsRegistered(entity.StatusCode))
            {
                await AddIncapacityJournalAsync(employeeRepository, personnelFile.Id, tenantId, entity, nowUtc, cancellationToken);
            }

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await incapacityRepository.GetResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity response could not be resolved after creation.");
            response = response with { Warnings = calculation.Warnings };

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Registered an incapacity for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileIncapacityResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    internal static async Task AddIncapacityJournalAsync(
        IPersonnelFileEmployeeRepository employeeRepository,
        long personnelFileId,
        Guid tenantId,
        PersonnelFileIncapacity entity,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var actionType = entity.ExtendsIncapacityId is null ? "INCAPACIDAD" : "PRORROGA_INCAPACIDAD";
        var action = PersonnelFilePersonnelAction.Create(
            actionType,
            "APLICADA",
            actionDateUtc: nowUtc,
            effectiveFromUtc: entity.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            effectiveToUtc: entity.EndDate is { } endDate ? endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null,
            description: $"{actionType} ({entity.RiskCodeSnapshot}) {entity.StartDate:yyyy-MM-dd}.",
            reference: null,
            amount: null,
            currencyCode: null,
            isSystemGenerated: true);
        action.BindToPersonnelFile(personnelFileId);
        action.SetTenantId(tenantId);
        _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);
    }
}

internal sealed class UpdatePersonnelFileIncapacityCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ILeaveCalculationDataProvider dataProvider,
    ICompanyPreferenceRepository companyPreferenceRepository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileIncapacityCommand, PersonnelFileIncapacityResponse>
{
    public async Task<Result<PersonnelFileIncapacityResponse>> Handle(
        UpdatePersonnelFileIncapacityCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageIncapacitiesAsync<PersonnelFileIncapacityResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await incapacityRepository.GetEntityAsync(personnelFile!.PublicId, command.IncapacityPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.StatusCode == IncapacityStatuses.Anulada)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.StateRuleViolation);
        }

        var item = command.Item;
        var datesChanged = entity.StartDate != item.StartDate || entity.EndDate != item.EndDate;
        if (datesChanged && await incapacityRepository.HasActiveExtensionsAsync(entity.Id, cancellationToken))
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.ChainLocked);
        }

        var tenantId = personnelFile.TenantId;

        var riskInternalId = await incapacityRepository.ResolveRiskInternalIdAsync(tenantId, item.RiskPublicId, cancellationToken);
        if (riskInternalId is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.RiskInvalid);
        }

        var typeInternalId = await incapacityRepository.ResolveIncapacityTypeInternalIdAsync(tenantId, item.IncapacityTypePublicId, cancellationToken);
        if (typeInternalId is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.TypeInvalid);
        }

        long? clinicInternalId = null;
        if (item.MedicalClinicPublicId is { } clinicPublicId)
        {
            clinicInternalId = await incapacityRepository.ResolveMedicalClinicInternalIdAsync(tenantId, clinicPublicId, cancellationToken);
            if (clinicInternalId is null)
            {
                return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.ClinicInvalid);
            }
        }

        long? payrollPeriodInternalId = null;
        if (item.PayrollPeriodDefinitionPublicId is { } payrollPeriodPublicId)
        {
            payrollPeriodInternalId = await incapacityRepository.ResolvePayrollPeriodInternalIdAsync(tenantId, payrollPeriodPublicId, cancellationToken);
            if (payrollPeriodInternalId is null)
            {
                return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.PayrollPeriodInvalid);
            }
        }

        var context = await dataProvider.GetCalculationContextAsync(
            tenantId, personnelFile.Id, item.AssignedPositionPublicId, riskInternalId.Value,
            item.StartDate, item.EndDate, excludeIncapacityId: entity.Id, extendsIncapacityId: entity.ExtendsIncapacityId, cancellationToken);
        if (context is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.RiskInvalid);
        }

        if (item.EndDate is null && !context.Risk.AllowsIndefinite)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.EndDateRequired);
        }

        if (context.MonthlyBaseSalary is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.BaseSalaryMissing);
        }

        if (await incapacityRepository.HasOverlappingIncapacityAsync(
                personnelFile.Id, item.StartDate, item.EndDate, excludeIncapacityId: entity.Id, cancellationToken))
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.Overlap);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var capTotal = IncapacityCalculationSupport.ResolveEmployerCapTotal(
            preference?.EmployerCoveredIncapacityDaysPerYear, preference?.AdditionalIncapacityBenefitDaysPerYear);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.UpdateDetails(
                riskInternalId.Value,
                context.Risk.Code,
                context.Risk.CountsSeventhDay,
                context.Risk.CountsSaturday,
                context.Risk.CountsHoliday,
                context.Risk.UsesFund,
                context.Risk.HasSubsidy,
                clinicInternalId,
                typeInternalId.Value,
                item.AssignedPositionPublicId,
                item.PayrollTypeCode,
                payrollPeriodInternalId,
                item.StartDate,
                item.EndDate,
                entity.ExtendsIncapacityId,
                item.Notes,
                context.Risk.AllowsIndefinite);

            var consumed = await incapacityRepository.GetRegisteredEmployerDaysConsumedAsync(
                personnelFile.Id, item.StartDate.Year, excludeIncapacityId: entity.Id, cancellationToken);
            var capRemaining = Math.Max(0, capTotal - consumed);
            var calculation = IncapacityCalculationSupport.Run(context, item.StartDate, item.EndDate, capRemaining);
            entity.ApplyCalculation(calculation.Snapshot);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await incapacityRepository.GetResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity response could not be resolved after update.");
            response = response with { Warnings = calculation.Warnings };

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Updated an incapacity for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileIncapacityResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ConfirmPersonnelFileIncapacityCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ILeaveCalculationDataProvider dataProvider,
    ICompanyPreferenceRepository companyPreferenceRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ConfirmPersonnelFileIncapacityCommand, PersonnelFileIncapacityResponse>
{
    public async Task<Result<PersonnelFileIncapacityResponse>> Handle(
        ConfirmPersonnelFileIncapacityCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageIncapacitiesAsync<PersonnelFileIncapacityResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // Anti-self (D-18): the employee cannot confirm their own registration.
        _ = Guid.TryParse(currentUserService.UserId, out var confirmedByUserId);
        if (personnelFile!.LinkedUserPublicId is { } subjectUserId && subjectUserId == confirmedByUserId)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.ConfirmSelfForbidden);
        }

        var entity = await incapacityRepository.GetEntityAsync(personnelFile.PublicId, command.IncapacityPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!IncapacityStatuses.Confirmable.Contains(entity.StatusCode))
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;
        var context = await dataProvider.GetCalculationContextAsync(
            tenantId, personnelFile.Id, entity.AssignedPositionPublicId, entity.IncapacityRiskId,
            entity.StartDate, entity.EndDate, excludeIncapacityId: entity.Id, extendsIncapacityId: entity.ExtendsIncapacityId, cancellationToken);
        if (context is null || context.MonthlyBaseSalary is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.BaseSalaryMissing);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var capTotal = IncapacityCalculationSupport.ResolveEmployerCapTotal(
            preference?.EmployerCoveredIncapacityDaysPerYear, preference?.AdditionalIncapacityBenefitDaysPerYear);
        var nowUtc = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Confirm(currentUserService.UserId ?? string.Empty, nowUtc);

            // R-T2: recompute the breakdown against the cap available at confirmation time.
            var consumed = await incapacityRepository.GetRegisteredEmployerDaysConsumedAsync(
                personnelFile.Id, entity.StartDate.Year, excludeIncapacityId: entity.Id, cancellationToken);
            var capRemaining = Math.Max(0, capTotal - consumed);
            var calculation = IncapacityCalculationSupport.Run(context, entity.StartDate, entity.EndDate, capRemaining);
            entity.ApplyCalculation(calculation.Snapshot);

            await AddPersonnelFileIncapacityCommandHandler.AddIncapacityJournalAsync(
                employeeRepository, personnelFile.Id, tenantId, entity, nowUtc, cancellationToken);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await incapacityRepository.GetResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity response could not be resolved after confirmation.");
            response = response with { Warnings = calculation.Warnings };

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Confirmed an incapacity for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileIncapacityResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ClosePersonnelFileIncapacityCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ILeaveCalculationDataProvider dataProvider,
    ICompanyPreferenceRepository companyPreferenceRepository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ClosePersonnelFileIncapacityCommand, PersonnelFileIncapacityResponse>
{
    public async Task<Result<PersonnelFileIncapacityResponse>> Handle(
        ClosePersonnelFileIncapacityCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageIncapacitiesAsync<PersonnelFileIncapacityResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await incapacityRepository.GetEntityAsync(personnelFile!.PublicId, command.IncapacityPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.StatusCode == IncapacityStatuses.Anulada || entity.EndDate is not null || command.EndDate < entity.StartDate)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;
        var context = await dataProvider.GetCalculationContextAsync(
            tenantId, personnelFile.Id, entity.AssignedPositionPublicId, entity.IncapacityRiskId,
            entity.StartDate, command.EndDate, excludeIncapacityId: entity.Id, extendsIncapacityId: entity.ExtendsIncapacityId, cancellationToken);
        if (context is null || context.MonthlyBaseSalary is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.BaseSalaryMissing);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var capTotal = IncapacityCalculationSupport.ResolveEmployerCapTotal(
            preference?.EmployerCoveredIncapacityDaysPerYear, preference?.AdditionalIncapacityBenefitDaysPerYear);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.CloseIndefinite(command.EndDate);

            var consumed = await incapacityRepository.GetRegisteredEmployerDaysConsumedAsync(
                personnelFile.Id, entity.StartDate.Year, excludeIncapacityId: entity.Id, cancellationToken);
            var capRemaining = Math.Max(0, capTotal - consumed);
            var calculation = IncapacityCalculationSupport.Run(context, entity.StartDate, command.EndDate, capRemaining);
            entity.ApplyCalculation(calculation.Snapshot);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await incapacityRepository.GetResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity response could not be resolved after closure.");
            response = response with { Warnings = calculation.Warnings };

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Closed an open-ended incapacity for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileIncapacityResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AnnulPersonnelFileIncapacityCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulPersonnelFileIncapacityCommand, PersonnelFileIncapacityResponse>
{
    public async Task<Result<PersonnelFileIncapacityResponse>> Handle(
        AnnulPersonnelFileIncapacityCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageIncapacitiesAsync<PersonnelFileIncapacityResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await incapacityRepository.GetEntityAsync(personnelFile!.PublicId, command.IncapacityPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!IncapacityStatuses.Annullable.Contains(entity.StatusCode))
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.StateRuleViolation);
        }

        // A source with live extensions must be annulled tail-first (aclaración №5).
        if (await incapacityRepository.HasActiveExtensionsAsync(entity.Id, cancellationToken))
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.ChainLocked);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Annul(command.Reason, dateTimeProvider.UtcNow);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await incapacityRepository.GetResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity response could not be resolved after annulment.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Annulled an incapacity for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileIncapacityResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileIncapacityExtensionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ILeaveCalculationDataProvider dataProvider,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IFileRepository fileRepository,
    IDocumentTypeCatalogRepository documentTypeCatalogRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileIncapacityExtensionCommand, PersonnelFileIncapacityResponse>
{
    public async Task<Result<PersonnelFileIncapacityResponse>> Handle(
        AddPersonnelFileIncapacityExtensionCommand command,
        CancellationToken cancellationToken)
    {
        // Extensions are HR-only (no self-service).
        var (failure, personnelFile) = await LoadForManageIncapacitiesAsync<PersonnelFileIncapacityResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var source = await incapacityRepository.GetEntityAsync(personnelFile.PublicId, command.SourceIncapacityPublicId, cancellationToken);
        if (source is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        // The source must be a settled, closed, non-annulled record (RN-04).
        if (source.StatusCode == IncapacityStatuses.Anulada
            || source.StatusCode == IncapacityStatuses.EnRevision
            || source.EndDate is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.ExtensionSourceInvalid);
        }

        var item = command.Item;
        var tenantId = personnelFile.TenantId;
        var startDate = source.EndDate.Value.AddDays(1);
        if (item.EndDate < startDate)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.ExtensionNotContiguous);
        }

        var riskInternalId = await incapacityRepository.ResolveRiskInternalIdAsync(tenantId, item.RiskPublicId, cancellationToken);
        if (riskInternalId is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.RiskInvalid);
        }

        var typeInternalId = await incapacityRepository.ResolveIncapacityTypeInternalIdAsync(tenantId, item.IncapacityTypePublicId, cancellationToken);
        if (typeInternalId is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.TypeInvalid);
        }

        long? clinicInternalId = null;
        if (item.MedicalClinicPublicId is { } clinicPublicId)
        {
            clinicInternalId = await incapacityRepository.ResolveMedicalClinicInternalIdAsync(tenantId, clinicPublicId, cancellationToken);
            if (clinicInternalId is null)
            {
                return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.ClinicInvalid);
            }
        }

        long? payrollPeriodInternalId = null;
        if (item.PayrollPeriodDefinitionPublicId is { } payrollPeriodPublicId)
        {
            payrollPeriodInternalId = await incapacityRepository.ResolvePayrollPeriodInternalIdAsync(tenantId, payrollPeriodPublicId, cancellationToken);
            if (payrollPeriodInternalId is null)
            {
                return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.PayrollPeriodInvalid);
            }
        }

        var context = await dataProvider.GetCalculationContextAsync(
            tenantId, personnelFile.Id, item.AssignedPositionPublicId, riskInternalId.Value,
            startDate, item.EndDate, excludeIncapacityId: null, extendsIncapacityId: source.Id, cancellationToken);
        if (context is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.RiskInvalid);
        }

        if (!context.Risk.AllowsExtension)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.ExtensionNotAllowed);
        }

        if (context.MonthlyBaseSalary is null)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.BaseSalaryMissing);
        }

        // The extension is contiguous with its source (start = source.end + 1) so it never overlaps the source,
        // but it must not collide with any OTHER live incapacity of the employee (RN-14).
        if (await incapacityRepository.HasOverlappingIncapacityAsync(
                personnelFile.Id, startDate, item.EndDate, excludeIncapacityId: source.Id, cancellationToken))
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(IncapacityErrors.Overlap);
        }

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var requiresDocument = preference?.IncapacityRequiresDocument ?? true;
        var documentResult = await IncapacityWriteSupport.ResolveDocumentAsync(
            item.DocumentFilePublicId, item.DocumentTypeCatalogItemPublicId, requiresDocument, tenantId,
            fileRepository, documentTypeCatalogRepository, cancellationToken);
        if (documentResult.IsFailure)
        {
            return Result<PersonnelFileIncapacityResponse>.Failure(documentResult.Error);
        }

        var entity = PersonnelFileIncapacity.Create(
            requesterFilePublicId: null,
            requesterNameSnapshot: null,
            requestedByUserId: currentUserService.UserId ?? string.Empty,
            originCode: IncapacityOrigins.Rrhh,
            incapacityRiskId: riskInternalId.Value,
            riskCodeSnapshot: context.Risk.Code,
            riskCountsSeventhDaySnapshot: context.Risk.CountsSeventhDay,
            riskCountsSaturdaySnapshot: context.Risk.CountsSaturday,
            riskCountsHolidaySnapshot: context.Risk.CountsHoliday,
            riskUsesFundSnapshot: context.Risk.UsesFund,
            riskHasSubsidySnapshot: context.Risk.HasSubsidy,
            medicalClinicId: clinicInternalId,
            incapacityTypeId: typeInternalId.Value,
            assignedPositionPublicId: item.AssignedPositionPublicId,
            payrollTypeCode: item.PayrollTypeCode,
            payrollPeriodDefinitionId: payrollPeriodInternalId,
            startDate: startDate,
            endDate: item.EndDate,
            extendsIncapacityId: source.Id,
            notes: item.Notes,
            riskAllowsIndefinite: context.Risk.AllowsIndefinite);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(tenantId);

        var capTotal = IncapacityCalculationSupport.ResolveEmployerCapTotal(
            preference?.EmployerCoveredIncapacityDaysPerYear, preference?.AdditionalIncapacityBenefitDaysPerYear);
        var nowUtc = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var consumed = await incapacityRepository.GetRegisteredEmployerDaysConsumedAsync(
                personnelFile.Id, startDate.Year, excludeIncapacityId: null, cancellationToken);
            var capRemaining = Math.Max(0, capTotal - consumed);
            var calculation = IncapacityCalculationSupport.Run(context, startDate, item.EndDate, capRemaining);
            entity.ApplyCalculation(calculation.Snapshot);
            incapacityRepository.Add(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            if (documentResult.Value is { } resolvedDocument)
            {
                var document = PersonnelFileIncapacityDocument.Create(
                    Guid.NewGuid(),
                    resolvedDocument.DocumentTypeInternalId,
                    resolvedDocument.StoredFile.PublicId,
                    resolvedDocument.StoredFile.FileName,
                    resolvedDocument.StoredFile.ContentType,
                    (int)resolvedDocument.StoredFile.SizeBytes,
                    item.DocumentObservations);
                document.BindToIncapacity(entity.Id);
                document.SetTenantId(tenantId);
                incapacityRepository.AddDocument(document);
            }

            await AddPersonnelFileIncapacityCommandHandler.AddIncapacityJournalAsync(
                employeeRepository, personnelFile.Id, tenantId, entity, nowUtc, cancellationToken);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await incapacityRepository.GetResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Incapacity extension response could not be resolved after creation.");
            response = response with { Warnings = calculation.Warnings };

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Registered an incapacity extension for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileIncapacityResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetPersonnelFileIncapacitiesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileIncapacitiesQuery, IReadOnlyCollection<PersonnelFileIncapacityResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileIncapacityResponse>>> Handle(
        GetPersonnelFileIncapacitiesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForIncapacityReadAsync<IReadOnlyCollection<PersonnelFileIncapacityResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await incapacityRepository.GetResponsesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileIncapacityResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileIncapacityByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileIncapacityByIdQuery, PersonnelFileIncapacityResponse>
{
    public async Task<Result<PersonnelFileIncapacityResponse>> Handle(
        GetPersonnelFileIncapacityByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForIncapacityReadAsync<PersonnelFileIncapacityResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await incapacityRepository.GetResponseAsync(personnelFile!.PublicId, query.IncapacityPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileIncapacityResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileIncapacityResponse>.Success(response);
    }
}

internal sealed class GetPersonnelFileIncapacityBalanceQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileIncapacityBalanceQuery, PersonnelFileIncapacityBalanceResponse>
{
    public async Task<Result<PersonnelFileIncapacityBalanceResponse>> Handle(
        GetPersonnelFileIncapacityBalanceQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForIncapacityReadAsync<PersonnelFileIncapacityBalanceResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var year = query.Year ?? dateTimeProvider.UtcNow.Year;
        var preference = await companyPreferenceRepository.GetByTenantIdAsync(personnelFile!.TenantId, cancellationToken);
        var consumed = await incapacityRepository.GetRegisteredEmployerDaysConsumedAsync(
            personnelFile.Id, year, excludeIncapacityId: null, cancellationToken);

        var balance = IncapacityBalanceRules.Compute(
            preference?.EmployerCoveredIncapacityDaysPerYear,
            preference?.AdditionalIncapacityBenefitDaysPerYear,
            consumed);

        return Result<PersonnelFileIncapacityBalanceResponse>.Success(new PersonnelFileIncapacityBalanceResponse(
            personnelFile.PublicId,
            year,
            balance.EmployerCoveredDays,
            balance.AdditionalBenefitDays,
            balance.TotalCapDays,
            balance.ConsumedEmployerDays,
            balance.RemainingDays));
    }
}
