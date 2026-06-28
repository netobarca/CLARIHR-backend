using CLARIHR.Application.Common.CQRS;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>A row of the company-wide certificate-request bandeja (D-08).</summary>
public sealed record CertificateRequestListItemResponse(
    Guid CertificateRequestPublicId,
    Guid PersonnelFilePublicId,
    string EmployeeFullName,
    string CertificateTypeCode,
    string? TypeName,
    string PurposeCode,
    string RequestStatusCode,
    string? AddressedTo,
    string DeliveryMethodCode,
    DateTime RequestDateUtc,
    DateTime? IssuedDateUtc,
    DateTime? DeliveredDateUtc,
    Guid? IssuedByUserId,
    int? ResponseTimeDays);

/// <summary>The company-wide bandeja page: items + paging + per-status counts (P-06).</summary>
public sealed record CertificateRequestBandejaResponse(
    IReadOnlyCollection<CertificateRequestListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

/// <summary>
/// An export row. The Excel/CSV writer turns the public property names into column headers (reflection), so the
/// property names are the Spanish headers seen by HR.
/// </summary>
public sealed record CertificateRequestExportRow(
    string Empleado,
    string Tipo,
    string Proposito,
    string Estado,
    string? DirigidaA,
    string MedioEntrega,
    DateTime FechaSolicitud,
    DateTime? FechaEmision,
    DateTime? FechaEntrega,
    int? TiempoRespuestaDias);

public sealed record QueryCertificateRequestsQuery(
    Guid CompanyId,
    string? TypeCode,
    string? StatusCode,
    string? PurposeCode,
    Guid? EmployeeId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Search,
    int PageNumber = 1,
    int PageSize = 25) : IQuery<CertificateRequestBandejaResponse>;

public sealed record ExportCertificateRequestsQuery(
    Guid CompanyId,
    string? TypeCode,
    string? StatusCode,
    string? PurposeCode,
    Guid? EmployeeId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Search,
    int? MaxRows) : IQuery<IReadOnlyCollection<CertificateRequestExportRow>>;

internal sealed class QueryCertificateRequestsQueryValidator : AbstractValidator<QueryCertificateRequestsQuery>
{
    public QueryCertificateRequestsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc!.Value)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
    }
}

internal sealed class ExportCertificateRequestsQueryValidator : AbstractValidator<ExportCertificateRequestsQuery>
{
    public ExportCertificateRequestsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc!.Value)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
    }
}
