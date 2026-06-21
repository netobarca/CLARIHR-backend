using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>One employment period in the derived timeline (RF-011). A period == one contract.</summary>
public sealed record EmploymentPeriodItem(
    int Sequence,
    DateTime StartDate,
    DateTime? EndDate,
    string ContractTypeCode,
    Guid? PositionSlotPublicId,
    bool IsCurrent,
    string? Notes);

public sealed record EmploymentPeriodsTimelineResponse(
    Guid PersonnelFileId,
    int PeriodCount,
    IReadOnlyCollection<EmploymentPeriodItem> Periods);

/// <summary>
/// RF-011 — read-only, chronologically ordered timeline of the employee's employment periods. It is
/// DERIVED from <c>PersonnelFileContractHistory</c> with no dedicated entity (D-14): each contract is
/// one period and the active contract is the current one. Every rehire adds a period.
/// </summary>
public sealed record GetEmploymentPeriodsTimelineQuery(Guid PersonnelFileId)
    : IQuery<EmploymentPeriodsTimelineResponse>;

internal sealed class GetEmploymentPeriodsTimelineQueryValidator : AbstractValidator<GetEmploymentPeriodsTimelineQuery>
{
    public GetEmploymentPeriodsTimelineQueryValidator()
    {
        RuleFor(query => query.PersonnelFileId).NotEmpty();
    }
}

internal sealed class GetEmploymentPeriodsTimelineQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetEmploymentPeriodsTimelineQuery, EmploymentPeriodsTimelineResponse>
{
    public async Task<Result<EmploymentPeriodsTimelineResponse>> Handle(
        GetEmploymentPeriodsTimelineQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForReadAsync<EmploymentPeriodsTimelineResponse>(
            query.PersonnelFileId,
            tenantContext,
            authorizationService,
            personnelFileRepository,
            cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // RF-011 / D-14 — derive the periods from contract history (the period backbone). Ordered
        // ascending so the current (active) period appears last and is flagged via IsCurrent.
        var contracts = await employeeRepository.GetContractHistoryAsync(personnelFile!.PublicId, cancellationToken);
        var periods = contracts
            .OrderBy(contract => contract.ContractDate)
            .ThenBy(contract => contract.ContractEndDate ?? DateTime.MaxValue)
            .Select((contract, index) => new EmploymentPeriodItem(
                index + 1,
                contract.ContractDate,
                contract.ContractEndDate,
                contract.ContractTypeCode,
                contract.PositionSlotId,
                contract.IsActive,
                contract.Notes))
            .ToArray();

        return Result<EmploymentPeriodsTimelineResponse>.Success(
            new EmploymentPeriodsTimelineResponse(personnelFile.PublicId, periods.Length, periods));
    }
}
