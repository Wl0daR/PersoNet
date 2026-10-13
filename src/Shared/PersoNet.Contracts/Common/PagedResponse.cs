namespace PersoNet.Contracts.Common;

/// <summary>
/// One page of a list endpoint result, as described in the API conventions.
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
