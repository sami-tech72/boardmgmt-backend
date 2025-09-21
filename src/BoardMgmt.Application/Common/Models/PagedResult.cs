namespace BoardMgmt.Application.Common.Models;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total);
