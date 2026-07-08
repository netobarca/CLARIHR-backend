using System.Text.Json;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

/// <summary>
/// EF-backed persistence port of the incapacities sub-resource (vacaciones/incapacidades PR-5). The projected
/// responses are materialized and mapped in-memory (the per-tranche <c>TrancheDetailJson</c> is deserialized —
/// not expressible in an EF projection), the tracked-entity loads feed the domain guards, and the
/// employer-cap consumption re-read backs both the balance and the in-transaction anti-race.
/// </summary>
internal sealed class PersonnelFileIncapacityRepository(ApplicationDbContext dbContext) : IPersonnelFileIncapacityRepository
{
    private static readonly JsonSerializerOptions TrancheOptions = new(JsonSerializerDefaults.Web);

    public async Task<long?> ResolveRiskInternalIdAsync(Guid tenantId, Guid riskPublicId, CancellationToken cancellationToken) =>
        await dbContext.IncapacityRisks
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == riskPublicId && item.IsActive)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<long?> ResolveIncapacityTypeInternalIdAsync(Guid tenantId, Guid typePublicId, CancellationToken cancellationToken) =>
        await dbContext.IncapacityTypes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == typePublicId && item.IsActive)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<long?> ResolveMedicalClinicInternalIdAsync(Guid tenantId, Guid clinicPublicId, CancellationToken cancellationToken) =>
        await dbContext.MedicalClinics
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == clinicPublicId && item.IsActive)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<long?> ResolvePayrollPeriodInternalIdAsync(Guid tenantId, Guid payrollPeriodPublicId, CancellationToken cancellationToken) =>
        await dbContext.PayrollPeriodDefinitions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == payrollPeriodPublicId && item.IsActive)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileIncapacityResponse>> GetResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken)
    {
        var items = await QueryWithIncludes()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId)
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.CreatedUtc)
            .ToListAsync(cancellationToken);
        return items.Select(MapResponse).ToArray();
    }

    public async Task<PersonnelFileIncapacityResponse?> GetResponseAsync(
        Guid personnelFilePublicId, Guid incapacityPublicId, CancellationToken cancellationToken)
    {
        var item = await QueryWithIncludes()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == incapacityPublicId)
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? null : MapResponse(item);
    }

    public async Task<PersonnelFileIncapacity?> GetEntityAsync(
        Guid personnelFilePublicId, Guid incapacityPublicId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileIncapacities
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == incapacityPublicId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> HasActiveExtensionsAsync(long incapacityId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileIncapacities
            .AsNoTracking()
            .AnyAsync(
                item => item.ExtendsIncapacityId == incapacityId && item.StatusCode != IncapacityStatuses.Anulada,
                cancellationToken);

    public async Task<bool> HasOverlappingIncapacityAsync(
        long personnelFileId, DateOnly startDate, DateOnly? endDate, long? excludeIncapacityId, CancellationToken cancellationToken)
    {
        var effectiveEnd = endDate ?? DateOnly.MaxValue;
        return await dbContext.PersonnelFileIncapacities
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId
                && item.StatusCode != IncapacityStatuses.Anulada
                && (!excludeIncapacityId.HasValue || item.Id != excludeIncapacityId.Value))
            // Two ranges overlap iff each starts on/before the other ends. Open-ended (null end) uses MaxValue.
            .AnyAsync(
                item => item.StartDate <= effectiveEnd
                    && startDate <= (item.EndDate ?? DateOnly.MaxValue),
                cancellationToken);
    }

    public async Task<int> GetRegisteredEmployerDaysConsumedAsync(
        long personnelFileId, int year, long? excludeIncapacityId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileIncapacities
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId
                && item.StatusCode == IncapacityStatuses.Registrada
                && item.StartDate.Year == year
                && (!excludeIncapacityId.HasValue || item.Id != excludeIncapacityId.Value))
            .SumAsync(item => (int?)item.EmployerDays, cancellationToken) ?? 0;

    public async Task<long?> GetInternalIdAsync(
        Guid personnelFilePublicId, Guid incapacityPublicId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileIncapacities
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == incapacityPublicId)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(PersonnelFileIncapacity entity) => dbContext.PersonnelFileIncapacities.Add(entity);

    public void AddDocument(PersonnelFileIncapacityDocument entity) => dbContext.PersonnelFileIncapacityDocuments.Add(entity);

    public async Task<IReadOnlyCollection<IncapacityDocumentResponse>> GetDocumentResponsesAsync(
        Guid incapacityPublicId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileIncapacityDocuments
            .AsNoTracking()
            .Where(document => document.Incapacity.PublicId == incapacityPublicId && document.IsActive)
            .OrderByDescending(document => document.CreatedUtc)
            .Select(document => MapDocument(document))
            .ToArrayAsync(cancellationToken);

    public async Task<IncapacityDocumentResponse?> GetDocumentResponseAsync(
        Guid incapacityPublicId, Guid documentPublicId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileIncapacityDocuments
            .AsNoTracking()
            .Where(document => document.Incapacity.PublicId == incapacityPublicId && document.PublicId == documentPublicId)
            .Select(document => MapDocument(document))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PersonnelFileIncapacityDocument?> GetDocumentEntityAsync(
        Guid incapacityPublicId, Guid documentPublicId, Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileIncapacityDocuments
            .SingleOrDefaultAsync(
                document => document.Incapacity.PublicId == incapacityPublicId
                    && document.PublicId == documentPublicId
                    && document.TenantId == tenantId,
                cancellationToken);

    private IQueryable<PersonnelFileIncapacity> QueryWithIncludes() =>
        dbContext.PersonnelFileIncapacities
            .AsNoTracking()
            .Include(item => item.IncapacityRisk)
            .Include(item => item.IncapacityType)
            .Include(item => item.MedicalClinic)
            .Include(item => item.PayrollPeriodDefinition)
            .Include(item => item.ExtendsIncapacity);

    private static PersonnelFileIncapacityResponse MapResponse(PersonnelFileIncapacity item) =>
        new(
            item.PublicId,
            item.RequesterFilePublicId,
            item.RequesterNameSnapshot,
            item.OriginCode,
            item.IncapacityRisk!.PublicId,
            item.RiskCodeSnapshot,
            item.IncapacityType!.PublicId,
            item.IncapacityType!.Code,
            item.MedicalClinic != null ? item.MedicalClinic.PublicId : (Guid?)null,
            item.AssignedPositionPublicId,
            item.PayrollTypeCode,
            item.PayrollPeriodDefinition != null ? item.PayrollPeriodDefinition.PublicId : (Guid?)null,
            item.ExtendsIncapacity != null ? item.ExtendsIncapacity.PublicId : (Guid?)null,
            item.StartDate,
            item.EndDate,
            item.CalendarDays,
            item.ComputableDays,
            item.ComputableDaysOverridden,
            item.OverrideNote,
            item.SubsidizedDays,
            item.DiscountDays,
            item.EmployerDays,
            item.MonthlyBaseSalary,
            item.DailySalary,
            item.SubsidyAmount,
            item.DiscountAmount,
            item.EmployerAmount,
            DeserializeTranches(item.TrancheDetailJson),
            item.StatusCode,
            item.Notes,
            item.IsActive,
            item.ConcurrencyToken,
            item.CreatedUtc,
            item.ModifiedUtc,
            []);

    private static IncapacityDocumentResponse MapDocument(PersonnelFileIncapacityDocument document) =>
        new(
            document.PublicId,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.PublicId : (Guid?)null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Code : null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Name : null,
            document.Observations,
            document.FilePublicId,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.IsActive,
            document.ConcurrencyToken,
            document.CreatedUtc,
            document.ModifiedUtc);

    private static IReadOnlyList<IncapacityTrancheResponse> DeserializeTranches(string? trancheDetailJson)
    {
        if (string.IsNullOrWhiteSpace(trancheDetailJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<IncapacityTrancheResponse>>(trancheDetailJson, TrancheOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
