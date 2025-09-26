using BoardMgmt.Application.Dashboard.DTOs;

namespace BoardMgmt.Application.Common.Interfaces.Repositories;

public interface IVoteReadRepository
{
    Task<int> CountPendingAsync(CancellationToken ct);

    // NEW: detail list for pending votes
    Task<(int total, IReadOnlyList<VoteItemDto> items)> GetPendingPagedAsync(int page, int pageSize, CancellationToken ct);
}
