using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Leave;

internal sealed class MedicalClinicRepository(ApplicationDbContext dbContext) : IMedicalClinicRepository
{
    public void Add(MedicalClinic medicalClinic) => dbContext.MedicalClinics.Add(medicalClinic);

    public Task<MedicalClinic?> GetByIdAsync(Guid medicalClinicId, CancellationToken cancellationToken) =>
        dbContext.MedicalClinics.SingleOrDefaultAsync(clinic => clinic.PublicId == medicalClinicId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid medicalClinicId, CancellationToken cancellationToken) =>
        dbContext.MedicalClinics
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(clinic => clinic.PublicId == medicalClinicId, cancellationToken);

    public Task<bool> DescriptionExistsAsync(
        Guid tenantId,
        string normalizedDescription,
        Guid? excludingMedicalClinicId,
        CancellationToken cancellationToken) =>
        dbContext.MedicalClinics.AnyAsync(
            clinic => clinic.TenantId == tenantId &&
                      clinic.NormalizedDescription == normalizedDescription &&
                      (!excludingMedicalClinicId.HasValue || clinic.PublicId != excludingMedicalClinicId.Value),
            cancellationToken);

    public async Task<PagedResponse<MedicalClinicListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? sectorCode,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MedicalClinics
            .AsNoTracking()
            .Where(clinic => clinic.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(clinic => clinic.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(sectorCode))
        {
            var normalizedSectorCode = sectorCode.Trim().ToUpperInvariant();
            query = query.Where(clinic => clinic.SectorCode == normalizedSectorCode);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(clinic => clinic.NormalizedDescription.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(clinic => clinic.Description)
            .ThenBy(clinic => clinic.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(clinic => new MedicalClinicListItemResponse(
                clinic.PublicId,
                clinic.Description,
                clinic.Specialty,
                clinic.SectorCode,
                clinic.IsActive,
                clinic.ConcurrencyToken,
                clinic.CreatedUtc,
                clinic.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<MedicalClinicListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<MedicalClinicResponse?> GetResponseByIdAsync(Guid medicalClinicId, CancellationToken cancellationToken) =>
        dbContext.MedicalClinics
            .AsNoTracking()
            .Where(clinic => clinic.PublicId == medicalClinicId)
            .Select(clinic => new MedicalClinicResponse(
                clinic.PublicId,
                clinic.Description,
                clinic.Specialty,
                clinic.SectorCode,
                clinic.IsActive,
                clinic.ConcurrencyToken,
                clinic.CreatedUtc,
                clinic.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}
