using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// The indebtedness READS (REQ-010 PR-3). Both handlers are queries and NEITHER injects <c>IUnitOfWork</c>: the
/// levantamiento says the simulation "no debe afectar la planilla", and the cheapest way to guarantee that is to
/// make it structurally impossible to write.
/// </summary>
internal sealed class GetIndebtednessQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetIndebtednessQuery, IndebtednessResponse>
{
    public async Task<Result<IndebtednessResponse>> Handle(
        GetIndebtednessQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadCompletedEmployeeForIndebtednessReadAsync<IndebtednessResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var snapshot = await employeeRepository.GetIndebtednessSnapshotAsync(
            file!.TenantId, file.Id, cancellationToken);

        // Nothing is being added: this is the employee's standing today.
        var assessment = IndebtednessRules.Assess(
            IndebtednessRules.ComputeBaseIncome(snapshot.BaseItems),
            snapshot.LoadItems,
            newInstallment: null,
            newInstallmentFrequencyCode: null,
            snapshot.GlobalLimitPercent,
            snapshot.LimitsByType,
            candidateTypeCode: null);

        var baseBreakdown = snapshot.BaseItems
            .Select(item => new IndebtednessBaseLineResponse(
                item.AssignedPositionPublicId,
                item.ConceptTypeCode,
                item.Value,
                item.PayPeriodCode,
                IndebtednessRules.Monthlyize(item.Value, item.PayPeriodCode)))
            .ToArray();

        var loadBreakdown = snapshot.LoadItems
            .Select(item =>
            {
                // Each row shows the ceiling that governs IT — which is what makes the screen actionable: the
                // operator sees WHY a particular credit is the one that broke the limit.
                var (limitPercent, limitSource) = IndebtednessRules.ResolveLimit(
                    item.RecurringDeductionTypeCode, snapshot.GlobalLimitPercent, snapshot.LimitsByType);

                return new IndebtednessLoadLineResponse(
                    item.RecurringDeductionPublicId,
                    item.RecurringDeductionTypeCode,
                    item.FinancialInstitution,
                    item.Reference,
                    item.InstallmentAmount,
                    item.InstallmentFrequencyCode,
                    IndebtednessRules.Monthlyize(item.InstallmentAmount, item.InstallmentFrequencyCode),
                    item.StatusCode,
                    item.IsIncludedInLoad,
                    limitPercent,
                    limitSource);
            })
            .ToArray();

        var overrides = await employeeRepository.GetIndebtednessOverridesAsync(
            file.TenantId, file.Id, cancellationToken);

        return Result<IndebtednessResponse>.Success(new IndebtednessResponse(
            file.PublicId,
            assessment.BaseIncome,
            baseBreakdown,
            assessment.CurrentLoad,
            loadBreakdown,
            assessment.ProjectedPercent,
            snapshot.GlobalLimitPercent,
            snapshot.LimitsByType,
            assessment.Status,
            overrides));
    }
}

internal sealed class SimulateIndebtednessQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<SimulateIndebtednessQuery, IndebtednessSimulationResponse>
{
    public async Task<Result<IndebtednessSimulationResponse>> Handle(
        SimulateIndebtednessQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, file) = await LoadCompletedEmployeeForIndebtednessReadAsync<IndebtednessSimulationResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var snapshot = await employeeRepository.GetIndebtednessSnapshotAsync(
            file!.TenantId, file.Id, cancellationToken);

        // The "ingreso digitado" of the levantamiento: the operator may try the numbers against a hypothetical
        // salary. Omitted ⇒ the derived one.
        var baseIncome = query.BaseIncomeOverride ?? IndebtednessRules.ComputeBaseIncome(snapshot.BaseItems);

        var current = IndebtednessRules.Assess(
            baseIncome, snapshot.LoadItems, null, null,
            snapshot.GlobalLimitPercent, snapshot.LimitsByType, query.AdditionalDeduction.TypeCode);

        var simulated = IndebtednessRules.Assess(
            baseIncome,
            snapshot.LoadItems,
            query.AdditionalDeduction.Amount,
            query.AdditionalDeduction.PayPeriodCode,
            snapshot.GlobalLimitPercent,
            snapshot.LimitsByType,
            query.AdditionalDeduction.TypeCode);

        return Result<IndebtednessSimulationResponse>.Success(new IndebtednessSimulationResponse(
            simulated.BaseIncome,
            simulated.CurrentLoad,
            current.ProjectedPercent,
            simulated.NewInstallment,
            simulated.ProjectedPercent,
            simulated.LimitPercent,
            simulated.LimitSource,
            simulated.IsExceeded,
            simulated.Status));
    }
}
