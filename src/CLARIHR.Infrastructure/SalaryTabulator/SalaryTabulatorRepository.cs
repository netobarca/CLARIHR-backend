using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.SalaryTabulator;
using CLARIHR.Domain.SalaryTabulator;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.SalaryTabulator;

internal sealed class SalaryTabulatorRepository(ApplicationDbContext dbContext) : ISalaryTabulatorRepository
{
    public void AddLine(SalaryTabulatorLine line) => dbContext.SalaryTabulatorLines.Add(line);

    public void AddChangeRequest(SalaryTabulatorChangeRequest request) => dbContext.SalaryTabulatorChangeRequests.Add(request);

    public Task<SalaryTabulatorLine?> GetLineByIdAsync(Guid lineId, CancellationToken cancellationToken) =>
        dbContext.SalaryTabulatorLines.SingleOrDefaultAsync(line => line.PublicId == lineId, cancellationToken);

    public Task<bool> LineExistsOutsideTenantAsync(Guid lineId, CancellationToken cancellationToken) =>
        dbContext.SalaryTabulatorLines
            .IgnoreQueryFilters()
            .AnyAsync(line => line.PublicId == lineId, cancellationToken);

    public Task<SalaryTabulatorLineResponse?> GetLineResponseByIdAsync(Guid lineId, CancellationToken cancellationToken) =>
        dbContext.SalaryTabulatorLines
            .AsNoTracking()
            .Where(line => line.PublicId == lineId)
            .Select(line => new SalaryTabulatorLineResponse(
                line.PublicId,
                line.TenantId,
                line.SalaryClassCode,
                line.SalaryScaleCode,
                line.CurrencyCode,
                line.BaseAmount,
                line.MinAmount,
                line.MaxAmount,
                line.EffectiveFromUtc,
                line.EffectiveToUtc,
                line.IsActive,
                line.Version,
                line.Notes,
                line.ConcurrencyToken,
                line.CreatedUtc,
                line.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<SalaryTabulatorLineListItemResponse>> SearchLinesAsync(
        Guid tenantId,
        string? salaryClassCode,
        string? salaryScaleCode,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SalaryTabulatorLines
            .AsNoTracking()
            .Where(line => line.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(salaryClassCode))
        {
            var normalized = salaryClassCode.Trim().ToUpperInvariant();
            query = query.Where(line => line.NormalizedSalaryClassCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(salaryScaleCode))
        {
            var normalized = salaryScaleCode.Trim().ToUpperInvariant();
            query = query.Where(line => line.NormalizedSalaryScaleCode == normalized);
        }

        if (isActive.HasValue)
        {
            query = query.Where(line => line.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(line =>
                line.NormalizedSalaryClassCode.Contains(normalizedSearch) ||
                line.NormalizedSalaryScaleCode.Contains(normalizedSearch) ||
                line.CurrencyCode.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(line => line.SalaryClassCode)
            .ThenBy(line => line.SalaryScaleCode)
            .ThenByDescending(line => line.EffectiveFromUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(line => new SalaryTabulatorLineListItemResponse(
                line.PublicId,
                line.SalaryClassCode,
                line.SalaryScaleCode,
                line.CurrencyCode,
                line.BaseAmount,
                line.MinAmount,
                line.MaxAmount,
                line.EffectiveFromUtc,
                line.EffectiveToUtc,
                line.IsActive,
                line.Version,
                line.ConcurrencyToken,
                line.CreatedUtc,
                line.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<SalaryTabulatorLineListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<IReadOnlyCollection<SalaryTabulatorLineExportRow>> GetLineExportRowsAsync(
        Guid tenantId,
        string? salaryClassCode,
        string? salaryScaleCode,
        bool? isActive,
        string? search,
        CancellationToken cancellationToken) =>
        GetLineExportRowsAsyncInternal(tenantId, salaryClassCode, salaryScaleCode, isActive, search, cancellationToken);

    private async Task<IReadOnlyCollection<SalaryTabulatorLineExportRow>> GetLineExportRowsAsyncInternal(
        Guid tenantId,
        string? salaryClassCode,
        string? salaryScaleCode,
        bool? isActive,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SalaryTabulatorLines
            .AsNoTracking()
            .Where(line => line.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(salaryClassCode))
        {
            var normalized = salaryClassCode.Trim().ToUpperInvariant();
            query = query.Where(line => line.NormalizedSalaryClassCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(salaryScaleCode))
        {
            var normalized = salaryScaleCode.Trim().ToUpperInvariant();
            query = query.Where(line => line.NormalizedSalaryScaleCode == normalized);
        }

        if (isActive.HasValue)
        {
            query = query.Where(line => line.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(line =>
                line.NormalizedSalaryClassCode.Contains(normalizedSearch) ||
                line.NormalizedSalaryScaleCode.Contains(normalizedSearch) ||
                line.CurrencyCode.Contains(normalizedSearch));
        }

        return await query
            .OrderBy(line => line.SalaryClassCode)
            .ThenBy(line => line.SalaryScaleCode)
            .ThenByDescending(line => line.EffectiveFromUtc)
            .Select(line => new SalaryTabulatorLineExportRow(
                line.PublicId,
                line.SalaryClassCode,
                line.SalaryScaleCode,
                line.CurrencyCode,
                line.BaseAmount,
                line.MinAmount,
                line.MaxAmount,
                line.EffectiveFromUtc,
                line.EffectiveToUtc,
                line.IsActive,
                line.Version,
                line.Notes,
                line.CreatedUtc,
                line.ModifiedUtc))
            .ToArrayAsync(cancellationToken);
    }

    public Task<SalaryTabulatorLineSnapshot?> GetActiveLineSnapshotAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken) =>
        dbContext.SalaryTabulatorLines
            .AsNoTracking()
            .Where(line =>
                line.TenantId == tenantId &&
                line.NormalizedSalaryClassCode == normalizedSalaryClassCode &&
                line.NormalizedSalaryScaleCode == normalizedSalaryScaleCode &&
                line.EffectiveFromUtc <= effectiveAtUtc &&
                (!line.EffectiveToUtc.HasValue || line.EffectiveToUtc.Value >= effectiveAtUtc))
            .OrderByDescending(line => line.EffectiveFromUtc)
            .Select(line => new SalaryTabulatorLineSnapshot(
                line.PublicId,
                line.CurrencyCode,
                line.BaseAmount,
                line.MinAmount,
                line.MaxAmount,
                line.EffectiveFromUtc,
                line.EffectiveToUtc))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<SalaryTabulatorLine?> GetActiveLineEntityAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken) =>
        dbContext.SalaryTabulatorLines
            .Where(line =>
                line.TenantId == tenantId &&
                line.NormalizedSalaryClassCode == normalizedSalaryClassCode &&
                line.NormalizedSalaryScaleCode == normalizedSalaryScaleCode &&
                line.EffectiveFromUtc <= effectiveAtUtc &&
                (!line.EffectiveToUtc.HasValue || line.EffectiveToUtc.Value >= effectiveAtUtc))
            .OrderByDescending(line => line.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasLineWithEffectiveFromOnOrAfterAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime effectiveFromUtc,
        long? excludingLineId,
        CancellationToken cancellationToken) =>
        dbContext.SalaryTabulatorLines
            .AsNoTracking()
            .AnyAsync(line =>
                line.TenantId == tenantId &&
                line.NormalizedSalaryClassCode == normalizedSalaryClassCode &&
                line.NormalizedSalaryScaleCode == normalizedSalaryScaleCode &&
                line.EffectiveFromUtc >= effectiveFromUtc &&
                (!excludingLineId.HasValue || line.Id != excludingLineId.Value),
                cancellationToken);

    public Task<SalaryTabulatorChangeRequest?> GetChangeRequestByIdAsync(Guid requestId, CancellationToken cancellationToken) =>
        dbContext.SalaryTabulatorChangeRequests
            .Include(request => request.Items)
            .SingleOrDefaultAsync(request => request.PublicId == requestId, cancellationToken);

    public Task<bool> ChangeRequestExistsOutsideTenantAsync(Guid requestId, CancellationToken cancellationToken) =>
        dbContext.SalaryTabulatorChangeRequests
            .IgnoreQueryFilters()
            .AnyAsync(request => request.PublicId == requestId, cancellationToken);

    public async Task<SalaryTabulatorChangeRequestResponse?> GetChangeRequestResponseByIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.SalaryTabulatorChangeRequests
            .AsNoTracking()
            .Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.PublicId == requestId, cancellationToken);

        return request is null ? null : MapRequestResponse(request);
    }

    public async Task<SalaryTabulatorChangeRequestImpactResponse?> GetChangeRequestImpactByIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.SalaryTabulatorChangeRequests
            .AsNoTracking()
            .Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.PublicId == requestId, cancellationToken);

        if (request is null)
        {
            return null;
        }

        var impactItems = request.Items
            .Select(item =>
            {
                var baseDelta = item.ChangeType switch
                {
                    SalaryTabulatorChangeType.Create => item.ProposedBaseAmount ?? 0,
                    SalaryTabulatorChangeType.Update => (item.ProposedBaseAmount ?? 0) - (item.CurrentBaseAmount ?? 0),
                    SalaryTabulatorChangeType.Inactivate => -(item.CurrentBaseAmount ?? 0),
                    _ => 0
                };

                return new SalaryTabulatorChangeRequestImpactItemResponse(
                    item.Id,
                    item.SalaryClassCode,
                    item.SalaryScaleCode,
                    item.ChangeType,
                    item.CurrentBaseAmount,
                    item.ProposedBaseAmount,
                    baseDelta);
            })
            .ToArray();

        var totalMonthlyDelta = impactItems.Sum(item => item.BaseDelta);

        return new SalaryTabulatorChangeRequestImpactResponse(
            request.PublicId,
            request.RequestNumber,
            request.Status,
            request.EffectiveFromUtc,
            impactItems.Length,
            totalMonthlyDelta,
            totalMonthlyDelta * 12,
            impactItems);
    }

    public async Task<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>> SearchChangeRequestsAsync(
        Guid tenantId,
        SalaryTabulatorChangeRequestStatus? status,
        Guid? requestedByUserId,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SalaryTabulatorChangeRequests
            .AsNoTracking()
            .Where(request => request.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        if (requestedByUserId.HasValue)
        {
            query = query.Where(request => request.RequestedByUserId == requestedByUserId.Value);
        }

        if (effectiveFromUtc.HasValue)
        {
            query = query.Where(request => request.EffectiveFromUtc >= effectiveFromUtc.Value);
        }

        if (effectiveToUtc.HasValue)
        {
            query = query.Where(request => request.EffectiveFromUtc <= effectiveToUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(request => request.CreatedUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(request => new SalaryTabulatorChangeRequestListItemResponse(
                request.PublicId,
                request.RequestNumber,
                request.Status,
                request.EffectiveFromUtc,
                request.RequestedByUserId,
                request.SubmittedAtUtc,
                request.DecidedByUserId,
                request.DecidedAtUtc,
                request.ConcurrencyToken,
                request.CreatedUtc,
                request.ModifiedUtc,
                request.Items.Count))
            .ToListAsync(cancellationToken);

        return new PagedResponse<SalaryTabulatorChangeRequestListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    private static SalaryTabulatorChangeRequestResponse MapRequestResponse(SalaryTabulatorChangeRequest request)
    {
        var itemResponses = request.Items
            .OrderBy(item => item.SalaryClassCode)
            .ThenBy(item => item.SalaryScaleCode)
            .ThenBy(item => item.Id)
            .Select(item => new SalaryTabulatorChangeRequestItemResponse(
                item.Id,
                item.SalaryClassCode,
                item.SalaryScaleCode,
                item.CurrencyCode,
                item.ChangeType,
                item.CurrentBaseAmount,
                item.ProposedBaseAmount,
                item.CurrentMinAmount,
                item.ProposedMinAmount,
                item.CurrentMaxAmount,
                item.ProposedMaxAmount,
                item.Notes))
            .ToArray();

        return new SalaryTabulatorChangeRequestResponse(
            request.PublicId,
            request.TenantId,
            request.RequestNumber,
            request.Reason,
            request.Status,
            request.EffectiveFromUtc,
            request.RequestedByUserId,
            request.SubmittedAtUtc,
            request.DecidedByUserId,
            request.DecidedAtUtc,
            request.DecisionComment,
            request.ConcurrencyToken,
            request.CreatedUtc,
            request.ModifiedUtc,
            itemResponses);
    }
}
