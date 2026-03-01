namespace CLARIHR.Application.Common.Pagination;

public sealed record PagedResponse<TItem>(
    IReadOnlyCollection<TItem> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);
