using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Default fund grants / bounds when neither the request nor the company preference supplies them (Art. 177).</summary>
internal static class VacationFundDefaults
{
    public const int LegalDays = 15;
    public const int BenefitDays = 0;
    public const bool UseAnniversary = true;
    public const bool GeneratesEnjoymentDays = true;
}

internal sealed class AddPersonnelFileVacationPeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileVacationRepository vacationRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileVacationPeriodCommand, PersonnelFileVacationPeriodResponse>
{
    public async Task<Result<PersonnelFileVacationPeriodResponse>> Handle(
        AddPersonnelFileVacationPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageVacationsAsync<PersonnelFileVacationPeriodResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileVacationPeriodResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var item = command.Item;
        var tenantId = personnelFile.TenantId;

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var legalDays = item.LegalDaysGranted ?? preference?.AnnualVacationDaysDefault ?? VacationFundDefaults.LegalDays;
        var benefitDays = item.BenefitDaysGranted ?? preference?.AdditionalVacationBenefitDaysDefault ?? VacationFundDefaults.BenefitDays;
        var useAnniversary = item.UseAnniversary ?? preference?.DefaultUseAnniversary ?? VacationFundDefaults.UseAnniversary;
        var generatesEnjoymentDays = item.GeneratesEnjoymentDays ?? VacationFundDefaults.GeneratesEnjoymentDays;

        var anchor = await vacationRepository.GetAnchorDateAsync(personnelFile.Id, cancellationToken);
        if (anchor is null)
        {
            return Result<PersonnelFileVacationPeriodResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var bounds = VacationRules.PeriodBounds(item.PeriodYear, useAnniversary, anchor.Value);
        if (!VacationRules.IsEligible(anchor.Value, bounds.Start))
        {
            return Result<PersonnelFileVacationPeriodResponse>.Failure(VacationErrors.EligibilityNotMet);
        }

        if (await vacationRepository.HasActivePeriodForYearAsync(personnelFile.Id, item.PeriodYear, excludePeriodId: null, cancellationToken))
        {
            return Result<PersonnelFileVacationPeriodResponse>.Failure(VacationErrors.PeriodDuplicate);
        }

        var entity = PersonnelFileVacationPeriod.Create(
            item.PeriodYear,
            bounds.Start,
            bounds.End,
            legalDays,
            benefitDays,
            generatesEnjoymentDays,
            useAnniversary,
            VacationPeriodSources.Manual);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(tenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            vacationRepository.AddPeriod(entity);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await vacationRepository.GetPeriodResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Vacation period response could not be resolved after creation.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Created a {entity.PeriodYear} vacation period for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileVacationPeriodResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileVacationPeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileVacationRepository vacationRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileVacationPeriodCommand, PersonnelFileVacationPeriodResponse>
{
    public async Task<Result<PersonnelFileVacationPeriodResponse>> Handle(
        UpdatePersonnelFileVacationPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageVacationsAsync<PersonnelFileVacationPeriodResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await vacationRepository.GetPeriodEntityAsync(personnelFile!.PublicId, command.VacationPeriodPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileVacationPeriodResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileVacationPeriodResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (await vacationRepository.HasConsumptionAsync(entity.Id, cancellationToken))
        {
            return Result<PersonnelFileVacationPeriodResponse>.Failure(VacationErrors.PeriodHasConsumption);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.UpdateGrants(command.Item.LegalDaysGranted, command.Item.BenefitDaysGranted);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await vacationRepository.GetPeriodResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Vacation period response could not be resolved after update.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Updated a vacation period of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileVacationPeriodResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DeletePersonnelFileVacationPeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileVacationRepository vacationRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileVacationPeriodCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileVacationPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageVacationsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await vacationRepository.GetPeriodEntityAsync(personnelFile!.PublicId, command.VacationPeriodPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (await vacationRepository.HasConsumptionAsync(entity.Id, cancellationToken))
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(VacationErrors.PeriodHasConsumption);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Deactivate();
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Removed a {entity.PeriodYear} vacation period from {personnelFile.FullName}.", null, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileParentConcurrencyResult>.Success(
                new PersonnelFileParentConcurrencyResult(personnelFile.ConcurrencyToken));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetPersonnelFileVacationPeriodsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileVacationRepository vacationRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileVacationPeriodsQuery, IReadOnlyCollection<PersonnelFileVacationPeriodResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileVacationPeriodResponse>>> Handle(
        GetPersonnelFileVacationPeriodsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForVacationReadAsync<IReadOnlyCollection<PersonnelFileVacationPeriodResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await vacationRepository.GetPeriodResponsesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileVacationPeriodResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileVacationPeriodByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileVacationRepository vacationRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileVacationPeriodByIdQuery, PersonnelFileVacationPeriodResponse>
{
    public async Task<Result<PersonnelFileVacationPeriodResponse>> Handle(
        GetPersonnelFileVacationPeriodByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForVacationReadAsync<PersonnelFileVacationPeriodResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await vacationRepository.GetPeriodResponseAsync(personnelFile!.PublicId, query.VacationPeriodPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileVacationPeriodResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileVacationPeriodResponse>.Success(response);
    }
}
