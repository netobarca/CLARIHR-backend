using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class QueryCompensatoryTimeMovementsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICompensatoryTimeRepository compensatoryTimeRepository)
    : IQueryHandler<QueryCompensatoryTimeMovementsQuery, CompensatoryTimeMovementBandejaResponse>
{
    public async Task<Result<CompensatoryTimeMovementBandejaResponse>> Handle(
        QueryCompensatoryTimeMovementsQuery query,
        CancellationToken cancellationToken)
    {
        // View gate enforced per handler (NOT a policy-set on the POST, which would treat this READ as a manage
        // action and 403 view-only users). ViewCompensatoryTime covers the company bandeja and its exports.
        var authorizationResult = await authorizationService.EnsureCanViewCompensatoryTimeAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompensatoryTimeMovementBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await compensatoryTimeRepository.QueryMovementsAsync(query, cancellationToken);
        return Result<CompensatoryTimeMovementBandejaResponse>.Success(page);
    }
}

internal sealed class ExportCompensatoryTimeMovementsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICompensatoryTimeRepository compensatoryTimeRepository)
    : IQueryHandler<ExportCompensatoryTimeMovementsQuery, IReadOnlyCollection<MovimientoTiempoCompensatorioExportRow>>
{
    public async Task<Result<IReadOnlyCollection<MovimientoTiempoCompensatorioExportRow>>> Handle(
        ExportCompensatoryTimeMovementsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewCompensatoryTimeAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<MovimientoTiempoCompensatorioExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await compensatoryTimeRepository.GetMovementExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<MovimientoTiempoCompensatorioExportRow>>.Success(rows);
    }
}

internal sealed class ExportCompensatoryTimeBalancesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ICompensatoryTimeRepository compensatoryTimeRepository)
    : IQueryHandler<ExportCompensatoryTimeBalancesQuery, IReadOnlyCollection<SaldoTiempoCompensatorioExportRow>>
{
    public async Task<Result<IReadOnlyCollection<SaldoTiempoCompensatorioExportRow>>> Handle(
        ExportCompensatoryTimeBalancesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewCompensatoryTimeAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<SaldoTiempoCompensatorioExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await compensatoryTimeRepository.GetBalanceExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<SaldoTiempoCompensatorioExportRow>>.Success(rows);
    }
}
