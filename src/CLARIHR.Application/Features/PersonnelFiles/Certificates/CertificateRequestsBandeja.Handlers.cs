using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

internal sealed class QueryCertificateRequestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<QueryCertificateRequestsQuery, CertificateRequestBandejaResponse>
{
    public async Task<Result<CertificateRequestBandejaResponse>> Handle(
        QueryCertificateRequestsQuery query,
        CancellationToken cancellationToken)
    {
        // Company-wide bandeja is HR-only (the employee uses the per-file list). D-08.
        var authorization = await authorizationService.EnsureCanViewCertificateRequestsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<CertificateRequestBandejaResponse>.Failure(authorization.Error);
        }

        var response = await employeeRepository.QueryCertificateRequestsAsync(
            query.CompanyId,
            query.TypeCode,
            query.StatusCode,
            query.PurposeCode,
            query.EmployeeId,
            query.FromUtc,
            query.ToUtc,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<CertificateRequestBandejaResponse>.Success(response);
    }
}

internal sealed class ExportCertificateRequestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportCertificateRequestsQuery, IReadOnlyCollection<CertificateRequestExportRow>>
{
    public async Task<Result<IReadOnlyCollection<CertificateRequestExportRow>>> Handle(
        ExportCertificateRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.EnsureCanViewCertificateRequestsAsync(query.CompanyId, cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<IReadOnlyCollection<CertificateRequestExportRow>>.Failure(authorization.Error);
        }

        var rows = await employeeRepository.GetCertificateRequestExportRowsAsync(
            query.CompanyId,
            query.TypeCode,
            query.StatusCode,
            query.PurposeCode,
            query.EmployeeId,
            query.FromUtc,
            query.ToUtc,
            query.Search,
            query.MaxRows,
            cancellationToken);

        return Result<IReadOnlyCollection<CertificateRequestExportRow>>.Success(rows);
    }
}
