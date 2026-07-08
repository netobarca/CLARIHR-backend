using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Abstractions.Leave;

public interface IMedicalClinicRepository
{
    void Add(MedicalClinic medicalClinic);

    Task<MedicalClinic?> GetByIdAsync(Guid medicalClinicId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid medicalClinicId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the (TenantId, NormalizedDescription) unique key. Pass the public id of
    /// the clinic being edited in <paramref name="excludingMedicalClinicId"/> to exclude itself.
    /// </summary>
    Task<bool> DescriptionExistsAsync(
        Guid tenantId,
        string normalizedDescription,
        Guid? excludingMedicalClinicId,
        CancellationToken cancellationToken);

    Task<PagedResponse<MedicalClinicListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? sectorCode,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<MedicalClinicResponse?> GetResponseByIdAsync(Guid medicalClinicId, CancellationToken cancellationToken);
}
