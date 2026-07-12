using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles.Absences;

internal sealed class QueryNotWorkedTimesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<QueryNotWorkedTimesQuery, NotWorkedTimeBandejaResponse>
{
    public async Task<Result<NotWorkedTimeBandejaResponse>> Handle(
        QueryNotWorkedTimesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewNotWorkedTimesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<NotWorkedTimeBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await employeeRepository.QueryNotWorkedTimesAsync(query, cancellationToken);
        return Result<NotWorkedTimeBandejaResponse>.Success(page);
    }
}

internal sealed class ExportNotWorkedTimesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportNotWorkedTimesQuery, IReadOnlyCollection<TiempoNoTrabajadoExportRow>>
{
    public async Task<Result<IReadOnlyCollection<TiempoNoTrabajadoExportRow>>> Handle(
        ExportNotWorkedTimesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewNotWorkedTimesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<TiempoNoTrabajadoExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await employeeRepository.GetNotWorkedTimeExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<TiempoNoTrabajadoExportRow>>.Success(rows);
    }
}

internal sealed class ExportNotWorkedTimePayrollInputQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportNotWorkedTimePayrollInputQuery, IReadOnlyCollection<InsumoPlanillaTiempoNoTrabajadoExportRow>>
{
    public async Task<Result<IReadOnlyCollection<InsumoPlanillaTiempoNoTrabajadoExportRow>>> Handle(
        ExportNotWorkedTimePayrollInputQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewNotWorkedTimesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaTiempoNoTrabajadoExportRow>>.Failure(authorizationResult.Error);
        }

        // The range is MANDATORY for the payroll input: a missing bound would silently dump the whole history into
        // a payroll run.
        if (query.StartDate is not { } startDate || query.EndDate is not { } endDate)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaTiempoNoTrabajadoExportRow>>.Failure(
                NotWorkedTimeErrors.PayrollInputRangeRequired);
        }

        var rows = await employeeRepository.GetNotWorkedTimePayrollInputRowsAsync(
            query.CompanyId, startDate, endDate, query.MaxRows, cancellationToken);
        return Result<IReadOnlyCollection<InsumoPlanillaTiempoNoTrabajadoExportRow>>.Success(rows);
    }
}
