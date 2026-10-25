using System.ComponentModel.DataAnnotations;

namespace OpsHub.Api.Common;

/// <summary>Query-string paging parameters shared by list endpoints (?page=2&amp;pageSize=20).</summary>
public class PageQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Capped so a client cannot request the whole table in one call.</summary>
    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    public int Skip => (Page - 1) * PageSize;
}

/// <summary>One page of results plus what the UI needs to draw pagination controls.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class LikePattern
{
    public const char EscapeCharacter = '\\';

    /// <summary>
    /// Builds a SQL LIKE pattern that matches <paramref name="term"/> anywhere, treating
    /// the user's % and _ as literal characters instead of wildcards.
    /// </summary>
    public static string Contains(string term)
    {
        var escaped = term
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
