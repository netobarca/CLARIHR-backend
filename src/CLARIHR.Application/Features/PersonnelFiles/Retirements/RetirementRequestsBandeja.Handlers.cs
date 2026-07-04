using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class QueryRetirementRequestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<QueryRetirementRequestsQuery, RetirementRequestBandejaResponse>
{
    public async Task<Result<RetirementRequestBandejaResponse>> Handle(
        QueryRetirementRequestsQuery query,
        CancellationToken cancellationToken)
    {
        // RRHH-only bandeja (RN-002.1, ratified: exclusive to HR — no manager/team visibility in Fase 1).
        var authorizationResult = await authorizationService.EnsureCanViewRetirementsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RetirementRequestBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await employeeRepository.QueryRetirementRequestsAsync(query, cancellationToken);
        return Result<RetirementRequestBandejaResponse>.Success(page);
    }
}

internal sealed class ExportRetirementRequestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportRetirementRequestsQuery, IReadOnlyCollection<RetirementRequestExportRow>>
{
    public async Task<Result<IReadOnlyCollection<RetirementRequestExportRow>>> Handle(
        ExportRetirementRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRetirementsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<RetirementRequestExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await employeeRepository.GetRetirementRequestExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<RetirementRequestExportRow>>.Success(rows);
    }
}
