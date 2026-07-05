using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class QuerySettlementsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ISettlementRepository settlementRepository)
    : IQueryHandler<QuerySettlementsQuery, SettlementBandejaResponse>
{
    public async Task<Result<SettlementBandejaResponse>> Handle(
        QuerySettlementsQuery query,
        CancellationToken cancellationToken)
    {
        // HR-only bandeja (D-20): settlements expose salary data, no self-service in Fase 1.
        var authorizationResult = await authorizationService.EnsureCanViewSettlementsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<SettlementBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await settlementRepository.QuerySettlementsAsync(query, cancellationToken);
        return Result<SettlementBandejaResponse>.Success(page);
    }
}

internal sealed class ExportSettlementsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    ISettlementRepository settlementRepository)
    : IQueryHandler<ExportSettlementsQuery, IReadOnlyCollection<SettlementExportRow>>
{
    public async Task<Result<IReadOnlyCollection<SettlementExportRow>>> Handle(
        ExportSettlementsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewSettlementsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<SettlementExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await settlementRepository.GetSettlementExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<SettlementExportRow>>.Success(rows);
    }
}
