namespace BoardMgmt.Application.Common.Interfaces.Repositories;

public interface IVoteReadRepository
{
    Task<int> CountPendingAsync(CancellationToken ct);
}
