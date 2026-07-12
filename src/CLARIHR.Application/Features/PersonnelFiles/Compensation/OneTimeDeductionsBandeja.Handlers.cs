using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class QueryOneTimeDeductionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<QueryOneTimeDeductionsQuery, OneTimeDeductionBandejaResponse>
{
    public async Task<Result<OneTimeDeductionBandejaResponse>> Handle(
        QueryOneTimeDeductionsQuery query,
        CancellationToken cancellationToken)
    {
        // View gate per handler (NOT a policy-set on the POST, which would treat this READ as a manage action).
        var authorizationResult = await authorizationService.EnsureCanViewOneTimeDeductionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OneTimeDeductionBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await employeeRepository.QueryOneTimeDeductionsAsync(query, cancellationToken);
        return Result<OneTimeDeductionBandejaResponse>.Success(page);
    }
}

internal sealed class ExportOneTimeDeductionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportOneTimeDeductionsQuery, IReadOnlyCollection<DescuentoEventualExportRow>>
{
    public async Task<Result<IReadOnlyCollection<DescuentoEventualExportRow>>> Handle(
        ExportOneTimeDeductionsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOneTimeDeductionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<DescuentoEventualExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await employeeRepository.GetOneTimeDeductionExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<DescuentoEventualExportRow>>.Success(rows);
    }
}

internal sealed class ExportOneTimeDeductionPayrollInputQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportOneTimeDeductionPayrollInputQuery, IReadOnlyCollection<InsumoPlanillaDescuentoEventualExportRow>>
{
    public async Task<Result<IReadOnlyCollection<InsumoPlanillaDescuentoEventualExportRow>>> Handle(
        ExportOneTimeDeductionPayrollInputQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOneTimeDeductionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaDescuentoEventualExportRow>>.Failure(authorizationResult.Error);
        }

        // The range is MANDATORY for the payroll input — a missing bound is a domain rule (422), not a shape error.
        if (query.StartDate is not { } startDate || query.EndDate is not { } endDate)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaDescuentoEventualExportRow>>.Failure(
                OneTimeDeductionErrors.PayrollInputRangeRequired);
        }

        var rows = await employeeRepository.GetOneTimeDeductionPayrollInputRowsAsync(
            query.CompanyId, query.PayrollTypeCode, startDate, endDate, query.MaxRows, cancellationToken);
        return Result<IReadOnlyCollection<InsumoPlanillaDescuentoEventualExportRow>>.Success(rows);
    }
}
