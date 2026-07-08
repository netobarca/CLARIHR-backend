using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class QueryIncapacitiesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileIncapacityRepository incapacityRepository)
    : IQueryHandler<QueryIncapacitiesQuery, IncapacityBandejaResponse>
{
    public async Task<Result<IncapacityBandejaResponse>> Handle(
        QueryIncapacitiesQuery query,
        CancellationToken cancellationToken)
    {
        // View gate enforced per handler (NOT a policy-set on the POST, which would treat this READ as a manage
        // action and 403 view-only users). ViewIncapacities covers the company bandeja and its exports.
        var authorizationResult = await authorizationService.EnsureCanViewIncapacitiesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IncapacityBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await incapacityRepository.QueryIncapacitiesAsync(query, cancellationToken);
        return Result<IncapacityBandejaResponse>.Success(page);
    }
}

internal sealed class ExportIncapacitiesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileIncapacityRepository incapacityRepository)
    : IQueryHandler<ExportIncapacitiesQuery, IReadOnlyCollection<IncapacidadExportRow>>
{
    public async Task<Result<IReadOnlyCollection<IncapacidadExportRow>>> Handle(
        ExportIncapacitiesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewIncapacitiesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<IncapacidadExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await incapacityRepository.GetIncapacityExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<IncapacidadExportRow>>.Success(rows);
    }
}
