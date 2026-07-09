using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

// The company reporting handlers enforce the family View gate PER HANDLER (the controller is intentionally NOT
// annotated with [AuthorizationPolicySet]; the convention would treat the POST query as a manage action and 403
// view-only users). ViewRecognitions / ViewDisciplinaryActions cover the bandejas, the exports and the payroll
// input.

internal sealed class QueryRecognitionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelTransactionRepository repository)
    : IQueryHandler<QueryRecognitionsQuery, RecognitionBandejaResponse>
{
    public async Task<Result<RecognitionBandejaResponse>> Handle(
        QueryRecognitionsQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRecognitionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecognitionBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await repository.QueryRecognitionsAsync(query, cancellationToken);
        return Result<RecognitionBandejaResponse>.Success(page);
    }
}

internal sealed class ExportRecognitionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelTransactionRepository repository)
    : IQueryHandler<ExportRecognitionsQuery, IReadOnlyCollection<ReconocimientoExportRow>>
{
    public async Task<Result<IReadOnlyCollection<ReconocimientoExportRow>>> Handle(
        ExportRecognitionsQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRecognitionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<ReconocimientoExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await repository.GetRecognitionExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<ReconocimientoExportRow>>.Success(rows);
    }
}

internal sealed class QueryDisciplinaryActionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelTransactionRepository repository)
    : IQueryHandler<QueryDisciplinaryActionsQuery, DisciplinaryActionBandejaResponse>
{
    public async Task<Result<DisciplinaryActionBandejaResponse>> Handle(
        QueryDisciplinaryActionsQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewDisciplinaryActionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DisciplinaryActionBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await repository.QueryDisciplinaryActionsAsync(query, cancellationToken);
        return Result<DisciplinaryActionBandejaResponse>.Success(page);
    }
}

internal sealed class ExportDisciplinaryActionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelTransactionRepository repository)
    : IQueryHandler<ExportDisciplinaryActionsQuery, IReadOnlyCollection<AmonestacionExportRow>>
{
    public async Task<Result<IReadOnlyCollection<AmonestacionExportRow>>> Handle(
        ExportDisciplinaryActionsQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewDisciplinaryActionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<AmonestacionExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await repository.GetDisciplinaryActionExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<AmonestacionExportRow>>.Success(rows);
    }
}

/// <summary>
/// The payroll input (RF-012): the ViewDisciplinaryActions gate, then the mandatory-range business check (422
/// <c>PERSONNEL_TRANSACTION_RANGE_REQUIRED</c> when a date is missing or start &gt; end), then the applied
/// effects of the range (one row per DESCUENTO / SUSPENSION_SIN_GOCE; revoked records excluded — RN-14/RN-15).
/// </summary>
internal sealed class ExportPayrollInputQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelTransactionRepository repository)
    : IQueryHandler<ExportPayrollInputQuery, IReadOnlyCollection<InsumoPlanillaExportRow>>
{
    public async Task<Result<IReadOnlyCollection<InsumoPlanillaExportRow>>> Handle(
        ExportPayrollInputQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewDisciplinaryActionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaExportRow>>.Failure(authorizationResult.Error);
        }

        if (query.StartDate is not { } startDate || query.EndDate is not { } endDate || endDate < startDate)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaExportRow>>.Failure(PersonnelTransactionReportingErrors.RangeRequired);
        }

        var rows = await repository.GetPayrollInputRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<InsumoPlanillaExportRow>>.Success(rows);
    }
}
