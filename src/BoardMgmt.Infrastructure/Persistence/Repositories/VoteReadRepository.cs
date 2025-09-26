using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Dashboard.DTOs;
using BoardMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Persistence.Repositories;

public class VoteReadRepository : IVoteReadRepository
{
    private readonly DbContext _db;
    public VoteReadRepository(DbContext db) => _db = db;

    // "Pending" = polls whose window is still open (deadline in the future)
    public Task<int> CountPendingAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return _db.Set<VotePoll>()
                  .Where(v => v.Deadline >= now)
                  .CountAsync(ct);
    }

    // OPTIONAL: if you later want "pending for a user" (open AND user hasn't voted):
    public Task<int> CountPendingForUserAsync(string userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        return _db.Set<VotePoll>()
            .Where(v => v.Deadline >= now)                              // still open
            .Where(v => !v.Ballots.Any(b => b.UserId == userId))        // user hasn't voted
                                                                        // If eligibility matters and you store it:
                                                                        // .Where(v => v.Eligibility != VoteEligibility.SpecificUsers
                                                                        //            || v.EligibleUsers.Any(eu => eu.UserId == userId))
            .CountAsync(ct);
    }


    public async Task<(int total, IReadOnlyList<VoteItemDto> items)> GetPendingPagedAsync(int page, int pageSize, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var baseQuery = _db.Set<VotePoll>().Where(v => v.Deadline >= now);

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderBy(v => v.Deadline)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VoteItemDto(v.Id, v.Title, v.Deadline.UtcDateTime))
            .ToListAsync(ct);

        return (total, items);
    }
}
