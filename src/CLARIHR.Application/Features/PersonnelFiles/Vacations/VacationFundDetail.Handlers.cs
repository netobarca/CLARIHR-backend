using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class GetPersonnelFileVacationFundQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileVacationRepository vacationRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileVacationFundQuery, VacationFundResponse>
{
    public async Task<Result<VacationFundResponse>> Handle(
        GetPersonnelFileVacationFundQuery query,
        CancellationToken cancellationToken)
    {
        // Fund is legible with ViewVacations OR the owner employee (D-18).
        var (failure, personnelFile) = await LoadCompletedEmployeeForVacationReadAsync<VacationFundResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var periods = await vacationRepository.GetActivePeriodConsumptionsAsync(personnelFile!.Id, cancellationToken);
        var monthlyBaseSalary = await vacationRepository.GetMonthlyBaseSalaryAsync(personnelFile.TenantId, personnelFile.Id, cancellationToken);
        var dailySalary = VacationFundMath.DailySalary(monthlyBaseSalary);

        var periodResponses = periods
            .Select(period =>
            {
                var granted = period.LegalDaysGranted + period.BenefitDaysGranted;
                var enjoyed = period.NetConsumedDays;
                var pending = Math.Max(0, granted - enjoyed);
                return new VacationFundPeriodResponse(
                    period.PublicId,
                    period.PeriodYear,
                    period.PeriodStartDate,
                    period.PeriodEndDate,
                    period.LegalDaysGranted,
                    period.BenefitDaysGranted,
                    granted,
                    period.GeneratesEnjoymentDays,
                    period.UsedAnniversary,
                    period.SourceCode,
                    enjoyed,
                    pending,
                    VacationFundMath.Provision(pending, dailySalary));
            })
            .ToArray();

        var totalGranted = periodResponses.Sum(period => period.TotalDaysGranted);
        var totalEnjoyed = periodResponses.Sum(period => period.EnjoyedDays);
        var totalPending = periodResponses.Sum(period => period.PendingDays);
        var totalProvision = dailySalary is null ? (decimal?)null : periodResponses.Sum(period => period.ProvisionAmount ?? 0m);

        return Result<VacationFundResponse>.Success(new VacationFundResponse(
            personnelFile.PublicId,
            dailySalary,
            totalGranted,
            totalEnjoyed,
            totalPending,
            totalProvision,
            periodResponses));
    }
}

internal sealed class ExportVacationFundQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileVacationRepository vacationRepository)
    : IQueryHandler<ExportVacationFundQuery, IReadOnlyCollection<FondoProvisionExportRow>>
{
    public async Task<Result<IReadOnlyCollection<FondoProvisionExportRow>>> Handle(
        ExportVacationFundQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewVacationsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<FondoProvisionExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await vacationRepository.GetFundProvisionRowsAsync(query.CompanyId, query.Year, query.MaxRows, cancellationToken);
        return Result<IReadOnlyCollection<FondoProvisionExportRow>>.Success(rows);
    }
}
