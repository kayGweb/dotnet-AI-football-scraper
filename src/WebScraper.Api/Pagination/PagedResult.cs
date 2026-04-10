namespace WebScraper.Api.Pagination;

/// <summary>
/// Standard paged response envelope. Used by list endpoints to return a page
/// slice plus total-count metadata — surfaced to clients both in the JSON body
/// and via the <c>X-Total-Count</c> header (see controller helpers).
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> From(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }
}

/// <summary>
/// Query parameters for paginated list endpoints. Page numbers are 1-based;
/// invalid values are silently clamped to the nearest valid range.
/// </summary>
public class PaginationQuery
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 200;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value,
        };
    }

    public int Skip => (Page - 1) * PageSize;
}
