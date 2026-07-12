using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

// The time-availability read is a corporate view with NO self-service branch (aclaración №9): the dedicated
// ViewTimeAvailability permission is enforced per handler (the query is a POST, so [AuthorizationPolicySet]
// would mis-classify it as a manage action — same reason the family bandejas gate per handler).

/// <summary>
/// The shared plumbing of the time-availability query/export: the mandatory-range validation (422), the
/// category-driven source selection, the concatenation of the F1 sources (suspensions + temporary-contract
/// ends) and the deterministic ordering (startDate asc, employee as tie-break). Connecting REQ-001/REQ-002 =
/// add a source method + a category to <see cref="TimeAvailabilityCategories"/> — this code does not change.
/// </summary>
internal static class TimeAvailabilitySupport
{
    /// <summary>Validates the mandatory range: null bound → RangeRequired; start &gt; end → RangeInvalid.</summary>
    public static Result<AvailabilityWindow> ResolveWindow(DateOnly? startDate, DateOnly? endDate)
    {
        if (startDate is not { } start || endDate is not { } end)
        {
            return Result<AvailabilityWindow>.Failure(TimeAvailabilityErrors.RangeRequired);
        }

        var window = PersonnelTransactionRules.BuildAvailabilityWindow(start, end);
        return window is { IsValid: true, Window: { } resolved }
            ? Result<AvailabilityWindow>.Success(resolved)
            : Result<AvailabilityWindow>.Failure(TimeAvailabilityErrors.RangeInvalid);
    }

    /// <summary>Normalizes the requested category codes (upper/trim, distinct); empty means "all sources".</summary>
    public static IReadOnlyCollection<string> NormalizeCategories(IReadOnlyCollection<string>? categoryCodes) =>
        categoryCodes is null
            ? []
            : categoryCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    /// <summary>A source is included when no category filter is set OR its category was requested.</summary>
    public static bool IsIncluded(IReadOnlyCollection<string> requested, string category) =>
        requested.Count == 0 || requested.Contains(category);

    /// <summary>Concatenates the included F1 sources into one ordered list (startDate asc, employee, reference).</summary>
    public static async Task<List<TimeAvailabilityRowResponse>> CollectRowsAsync(
        IPersonnelTransactionRepository repository,
        Guid companyId,
        AvailabilityWindow window,
        TimeAvailabilityFilters filters,
        IReadOnlyCollection<string> requestedCategories,
        CancellationToken cancellationToken)
    {
        var rows = new List<TimeAvailabilityRowResponse>();

        if (IsIncluded(requestedCategories, TimeAvailabilityCategories.Suspension))
        {
            rows.AddRange(await repository.GetSuspensionAvailabilityRowsAsync(companyId, window, filters, cancellationToken));
        }

        if (IsIncluded(requestedCategories, TimeAvailabilityCategories.TemporaryContractEnd))
        {
            rows.AddRange(await repository.GetTemporaryContractEndRowsAsync(companyId, window, filters, cancellationToken));
        }

        if (IsIncluded(requestedCategories, TimeAvailabilityCategories.NotWorkedTime))
        {
            rows.AddRange(await repository.GetNotWorkedTimeAvailabilityRowsAsync(companyId, window, filters, cancellationToken));
        }

        return rows
            .OrderBy(row => row.StartDate)
            .ThenBy(row => row.EmployeeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ReferencePublicId)
            .ToList();
    }
}

internal sealed class TimeAvailabilityQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelTransactionRepository repository)
    : IQueryHandler<TimeAvailabilityQuery, TimeAvailabilityQueryResponse>
{
    public async Task<Result<TimeAvailabilityQueryResponse>> Handle(
        TimeAvailabilityQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewTimeAvailabilityAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<TimeAvailabilityQueryResponse>.Failure(authorizationResult.Error);
        }

        var windowResult = TimeAvailabilitySupport.ResolveWindow(query.StartDate, query.EndDate);
        if (windowResult.IsFailure)
        {
            return Result<TimeAvailabilityQueryResponse>.Failure(windowResult.Error);
        }

        var requestedCategories = TimeAvailabilitySupport.NormalizeCategories(query.CategoryCodes);
        var filters = new TimeAvailabilityFilters(query.PersonnelFilePublicId, query.OrgUnitPublicId);

        var ordered = await TimeAvailabilitySupport.CollectRowsAsync(
            repository, query.CompanyId, windowResult.Value, filters, requestedCategories, cancellationToken);

        // categoryCounts always advertise the INCLUDED F1 categories (0 when they produced no rows) so the
        // frontend reads a stable shape; a category filtered out is absent.
        var categoryCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var category in TimeAvailabilityCategories.ActiveSources)
        {
            if (TimeAvailabilitySupport.IsIncluded(requestedCategories, category))
            {
                categoryCounts[category] = 0;
            }
        }

        foreach (var row in ordered)
        {
            categoryCounts[row.CategoryCode] = categoryCounts.TryGetValue(row.CategoryCode, out var count) ? count + 1 : 1;
        }

        var page = ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        var response = new TimeAvailabilityQueryResponse(
            page,
            query.PageNumber,
            query.PageSize,
            ordered.Count,
            categoryCounts,
            TimeAvailabilityCategories.ActiveSources);

        return Result<TimeAvailabilityQueryResponse>.Success(response);
    }
}

internal sealed class ExportTimeAvailabilityQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelTransactionRepository repository)
    : IQueryHandler<ExportTimeAvailabilityQuery, IReadOnlyCollection<DisponibilidadTiempoExportRow>>
{
    public async Task<Result<IReadOnlyCollection<DisponibilidadTiempoExportRow>>> Handle(
        ExportTimeAvailabilityQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewTimeAvailabilityAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<DisponibilidadTiempoExportRow>>.Failure(authorizationResult.Error);
        }

        var windowResult = TimeAvailabilitySupport.ResolveWindow(query.StartDate, query.EndDate);
        if (windowResult.IsFailure)
        {
            return Result<IReadOnlyCollection<DisponibilidadTiempoExportRow>>.Failure(windowResult.Error);
        }

        var requestedCategories = TimeAvailabilitySupport.NormalizeCategories(query.CategoryCodes);
        var filters = new TimeAvailabilityFilters(query.PersonnelFilePublicId, query.OrgUnitPublicId);

        var ordered = await TimeAvailabilitySupport.CollectRowsAsync(
            repository, query.CompanyId, windowResult.Value, filters, requestedCategories, cancellationToken);

        IEnumerable<TimeAvailabilityRowResponse> capped = query.MaxRows is { } maxRows ? ordered.Take(maxRows + 1) : ordered;

        var rows = capped
            .Select(row => new DisponibilidadTiempoExportRow(
                row.EmployeeName,
                row.EmployeeCode,
                row.PositionName,
                row.CategoryCode,
                row.StartDate,
                row.EndDate,
                row.Days,
                row.StatusCode,
                row.SourceModule))
            .ToArray();

        return Result<IReadOnlyCollection<DisponibilidadTiempoExportRow>>.Success(rows);
    }
}
