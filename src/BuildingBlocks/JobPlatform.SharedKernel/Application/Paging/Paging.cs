namespace JobPlatform.SharedKernel.Application.Paging;

public sealed record PageRequest
{
    public const int MaxPageSize = 100;

    public PageRequest(int page = 1, int pageSize = 20)
    {
        Page = page < 1 ? 1 : page;
        PageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);
    }

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
