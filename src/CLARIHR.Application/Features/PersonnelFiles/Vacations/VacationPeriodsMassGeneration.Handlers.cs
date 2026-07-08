using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Localization;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Company-wide idempotent generation of the yearly vacation fund (leave module §3.6): one active period per
/// active employee for the year. Re-runs create nothing for employees that already have an active period
/// (skipped); Art. 177-ineligible employees are reported per row (errors). Manage-only.
/// </summary>
internal sealed class GenerateVacationPeriodsCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileVacationRepository vacationRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IBackendMessageLocalizer localizer,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<GenerateVacationPeriodsCommand, VacationPeriodGenerationSummary>
{
    public async Task<Result<VacationPeriodGenerationSummary>> Handle(
        GenerateVacationPeriodsCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageVacationsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<VacationPeriodGenerationSummary>.Failure(authorizationResult.Error);
        }

        var tenantId = command.CompanyId;
        var item = command.Item;

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var legalDays = item.LegalDaysGranted ?? preference?.AnnualVacationDaysDefault ?? VacationFundDefaults.LegalDays;
        var benefitDays = item.BenefitDaysGranted ?? preference?.AdditionalVacationBenefitDaysDefault ?? VacationFundDefaults.BenefitDays;
        var useAnniversary = item.UseAnniversary ?? preference?.DefaultUseAnniversary ?? VacationFundDefaults.UseAnniversary;
        var generatesEnjoymentDays = item.GeneratesEnjoymentDays ?? VacationFundDefaults.GeneratesEnjoymentDays;

        var candidates = await vacationRepository.GetGenerationCandidatesAsync(tenantId, item.EmployeeIds, cancellationToken);
        var existing = await vacationRepository.GetPersonnelFileIdsWithActivePeriodForYearAsync(tenantId, item.Year, cancellationToken);

        var created = 0;
        var skipped = 0;
        var errors = new List<VacationPeriodGenerationError>();
        var toAdd = new List<PersonnelFileVacationPeriod>();

        foreach (var candidate in candidates)
        {
            if (existing.Contains(candidate.PersonnelFileId))
            {
                skipped++;
                continue;
            }

            var bounds = VacationRules.PeriodBounds(item.Year, useAnniversary, candidate.AnchorDate);
            if (!VacationRules.IsEligible(candidate.AnchorDate, bounds.Start))
            {
                errors.Add(new VacationPeriodGenerationError(
                    candidate.PublicId,
                    candidate.FullName,
                    VacationErrors.EligibilityNotMet.Code,
                    localizer.Localize(VacationErrors.EligibilityNotMet.Code, VacationErrors.EligibilityNotMet.Message)));
                continue;
            }

            var entity = PersonnelFileVacationPeriod.Create(
                item.Year,
                bounds.Start,
                bounds.End,
                legalDays,
                benefitDays,
                generatesEnjoymentDays,
                useAnniversary,
                VacationPeriodSources.MassGeneration);
            entity.BindToPersonnelFile(candidate.PersonnelFileId);
            entity.SetTenantId(tenantId);
            toAdd.Add(entity);
            created++;
        }

        var summary = new VacationPeriodGenerationSummary(item.Year, candidates.Count, created, skipped, errors);

        if (toAdd.Count == 0)
        {
            return Result<VacationPeriodGenerationSummary>.Success(summary);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var entity in toAdd)
            {
                vacationRepository.AddPeriod(entity);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.VacationPeriodsGenerated,
                    AuditEntityTypes.PersonnelFile,
                    command.CompanyId,
                    $"VACATION_PERIODS_{item.Year}",
                    AuditActions.Create,
                    $"Generated {created} vacation period(s) for {item.Year} ({skipped} skipped, {errors.Count} ineligible).",
                    After: summary),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<VacationPeriodGenerationSummary>.Success(summary);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
