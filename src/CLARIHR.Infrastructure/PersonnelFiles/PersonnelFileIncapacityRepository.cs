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

    public async Task<IncapacityBandejaResponse> QueryIncapacitiesAsync(
        QueryIncapacitiesQuery query, CancellationToken cancellationToken)
    {
        var baseQuery = FilteredIncapacities(
            query.EmployeeId, query.RiskCode, query.IncapacityTypeCode, query.PayrollTypeCode,
            query.StartFromUtc, query.StartToUtc);

        // StatusCounts over the full (non-status) filter, so every status is represented even though the items
        // default to REGISTRADA.
        var statusCounts = await baseQuery
            .GroupBy(row => row.Incapacity.StatusCode)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        var statusFilter = ResolveStatusFilter(query.StatusCode);
        var filtered = baseQuery.Where(row => row.Incapacity.StatusCode == statusFilter);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(row => row.Incapacity.StartDate)
            .ThenByDescending(row => row.Incapacity.CreatedUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new IncapacityListItemResponse(
                row.Incapacity.PublicId,
                row.EmployeeFilePublicId,
                row.EmployeeFullName,
                row.EmployeeCode,
                row.Incapacity.AssignedPositionPublicId,
                row.Incapacity.RiskCodeSnapshot,
                row.IncapacityTypePublicId,
                row.IncapacityTypeCode,
                row.IncapacityTypeName,
                row.MedicalClinicName,
                row.Incapacity.StatusCode,
                row.Incapacity.OriginCode,
                row.Incapacity.StartDate,
                row.Incapacity.EndDate,
                row.Incapacity.CalendarDays,
                row.Incapacity.ComputableDays,
                row.Incapacity.SubsidizedDays,
                row.Incapacity.DiscountDays,
                row.Incapacity.EmployerDays,
                row.Incapacity.SubsidyAmount,
                row.Incapacity.DiscountAmount,
                row.Incapacity.EmployerAmount,
                row.Incapacity.PayrollTypeCode,
                row.PayrollPeriodLabel,
                row.Incapacity.RiskUsesFundSnapshot))
            .ToArrayAsync(cancellationToken);

        return new IncapacityBandejaResponse(items, query.PageNumber, query.PageSize, totalCount, statusCounts);
    }

    public async Task<IReadOnlyCollection<IncapacidadExportRow>> GetIncapacityExportRowsAsync(
        ExportIncapacitiesQuery query, CancellationToken cancellationToken)
    {
        var statusFilter = ResolveStatusFilter(query.StatusCode);
        var filtered = FilteredIncapacities(
                query.EmployeeId, query.RiskCode, query.IncapacityTypeCode, query.PayrollTypeCode,
                query.StartFromUtc, query.StartToUtc)
            .Where(row => row.Incapacity.StatusCode == statusFilter)
            .OrderByDescending(row => row.Incapacity.StartDate)
            .ThenByDescending(row => row.Incapacity.CreatedUtc);

        var limited = query.MaxRows is { } maxRows ? filtered.Take(maxRows + 1) : filtered;

        // Materialized then mapped in-memory: TrancheDetailJson is deserialized and flattened (not expressible
        // in an EF projection).
        var rows = await limited.ToListAsync(cancellationToken);
        return rows
            .Select(row => new IncapacidadExportRow(
                row.EmployeeFullName,
                row.EmployeeCode,
                row.Incapacity.AssignedPositionPublicId?.ToString() ?? string.Empty,
                row.Incapacity.RiskCodeSnapshot,
                row.IncapacityTypeName ?? row.IncapacityTypeCode,
                row.MedicalClinicName,
                row.Incapacity.StatusCode,
                row.Incapacity.OriginCode,
                row.Incapacity.StartDate,
                row.Incapacity.EndDate,
                row.Incapacity.CalendarDays,
                row.Incapacity.ComputableDays,
                row.Incapacity.SubsidizedDays,
                row.Incapacity.DiscountDays,
                row.Incapacity.EmployerDays,
                row.Incapacity.SubsidyAmount,
                row.Incapacity.DiscountAmount,
                row.Incapacity.EmployerAmount,
                FlattenTranches(row.Incapacity.TrancheDetailJson),
                row.Incapacity.MonthlyBaseSalary,
                row.Incapacity.DailySalary,
                row.Incapacity.PayrollTypeCode,
                row.PayrollPeriodLabel,
                row.PayrollPeriodStart,
                row.PayrollPeriodEnd,
                row.Incapacity.RiskUsesFundSnapshot))
            .ToArray();
    }

    private IQueryable<IncapacityQueryRow> FilteredIncapacities(
        Guid? employeeId,
        string? riskCode,
        string? incapacityTypeCode,
        string? payrollTypeCode,
        DateTime? startFromUtc,
        DateTime? startToUtc)
    {
        // Member-init (not a positional record ctor) so EF composes further Where/GroupBy over this intermediate
        // projection reliably (same shape as SettlementRepository.FilteredSettlements).
        var query =
            from incapacity in dbContext.PersonnelFileIncapacities.AsNoTracking()
            join file in dbContext.PersonnelFiles.AsNoTracking() on incapacity.PersonnelFileId equals file.Id
            join type in dbContext.IncapacityTypes.AsNoTracking() on incapacity.IncapacityTypeId equals type.Id
            join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profileEntry.PersonnelFileId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            join clinicEntry in dbContext.MedicalClinics.AsNoTracking()
                on incapacity.MedicalClinicId equals (long?)clinicEntry.Id into clinicGroup
            from clinic in clinicGroup.DefaultIfEmpty()
            join periodEntry in dbContext.PayrollPeriodDefinitions.AsNoTracking()
                on incapacity.PayrollPeriodDefinitionId equals (long?)periodEntry.Id into periodGroup
            from period in periodGroup.DefaultIfEmpty()
            select new IncapacityQueryRow
            {
                Incapacity = incapacity,
                EmployeeFullName = file.FullName,
                EmployeeFilePublicId = file.PublicId,
                EmployeeCode = profile != null ? profile.EmployeeCode : null,
                IncapacityTypePublicId = type.PublicId,
                IncapacityTypeCode = type.Code,
                IncapacityTypeName = type.Name,
                MedicalClinicName = clinic != null ? clinic.Description : null,
                PayrollPeriodLabel = period != null ? period.Label : null,
                PayrollPeriodStart = period != null ? period.StartDate : (DateOnly?)null,
                PayrollPeriodEnd = period != null ? period.EndDate : (DateOnly?)null,
            };

        if (employeeId is { } employeePublicId)
        {
            query = query.Where(row => row.EmployeeFilePublicId == employeePublicId);
        }

        if (!string.IsNullOrWhiteSpace(riskCode))
        {
            var normalizedRisk = riskCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.Incapacity.RiskCodeSnapshot == normalizedRisk);
        }

        if (!string.IsNullOrWhiteSpace(incapacityTypeCode))
        {
            var normalizedType = incapacityTypeCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.IncapacityTypeCode == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(payrollTypeCode))
        {
            var normalizedPayrollType = payrollTypeCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.Incapacity.PayrollTypeCode == normalizedPayrollType);
        }

        if (startFromUtc is { } startFrom)
        {
            var from = DateOnly.FromDateTime(startFrom);
            query = query.Where(row => row.Incapacity.StartDate >= from);
        }

        if (startToUtc is { } startTo)
        {
            var to = DateOnly.FromDateTime(startTo);
            query = query.Where(row => row.Incapacity.StartDate <= to);
        }

        return query;
    }

    /// <summary>The bandeja/export default (R-T6): when no status is supplied the input is REGISTRADA only, which
    /// excludes the EN_REVISION self-registrations from the payroll input.</summary>
    private static string ResolveStatusFilter(string? statusCode) =>
        string.IsNullOrWhiteSpace(statusCode) ? IncapacityStatuses.Registrada : statusCode.Trim().ToUpperInvariant();

    private static string FlattenTranches(string? trancheDetailJson) =>
        string.Join(
            "; ",
            DeserializeTranches(trancheDetailJson)
                .Select(tranche => $"{tranche.DayFromAbsolute}-{tranche.DayToAbsolute}: {tranche.SubsidyPercent:0.##}% {tranche.PayerCode}"));

    private sealed class IncapacityQueryRow
    {
        public PersonnelFileIncapacity Incapacity { get; init; } = null!;

        public string EmployeeFullName { get; init; } = string.Empty;

        public Guid EmployeeFilePublicId { get; init; }

        public string? EmployeeCode { get; init; }

        public Guid IncapacityTypePublicId { get; init; }

        public string IncapacityTypeCode { get; init; } = string.Empty;

        public string? IncapacityTypeName { get; init; }

        public string? MedicalClinicName { get; init; }

        public string? PayrollPeriodLabel { get; init; }

        public DateOnly? PayrollPeriodStart { get; init; }

        public DateOnly? PayrollPeriodEnd { get; init; }
    }

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
