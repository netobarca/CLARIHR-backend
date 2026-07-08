using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class QueryVacationRequestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileVacationRepository vacationRepository)
    : IQueryHandler<QueryVacationRequestsQuery, VacationRequestBandejaResponse>
{
    public async Task<Result<VacationRequestBandejaResponse>> Handle(
        QueryVacationRequestsQuery query, CancellationToken cancellationToken)
    {
        // View gate enforced per handler (NOT a policy-set on the POST, which would treat this READ as a manage
        // action and 403 view-only users). ViewVacations covers the company bandeja and its export.
        var authorizationResult = await authorizationService.EnsureCanViewVacationsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<VacationRequestBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await vacationRepository.QueryRequestsAsync(query, cancellationToken);
        return Result<VacationRequestBandejaResponse>.Success(page);
    }
}

internal sealed class ExportVacationRequestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileVacationRepository vacationRepository)
    : IQueryHandler<ExportVacationRequestsQuery, IReadOnlyCollection<GoceVacacionesExportRow>>
{
    public async Task<Result<IReadOnlyCollection<GoceVacacionesExportRow>>> Handle(
        ExportVacationRequestsQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewVacationsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<GoceVacacionesExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await vacationRepository.GetGoceExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<GoceVacacionesExportRow>>.Success(rows);
    }
}
