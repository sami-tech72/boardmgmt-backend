using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Votes.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Votes.Queries;

public sealed record GetRecentVotesQuery : IRequest<IReadOnlyList<VoteSummaryDto>>;

public sealed class GetRecentVotesQueryHandler
    : IRequestHandler<GetRecentVotesQuery, IReadOnlyList<VoteSummaryDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _user;

    public GetRecentVotesQueryHandler(IAppDbContext db, ICurrentUser user)
    { _db = db; _user = user; }

    public async Task<IReadOnlyList<VoteSummaryDto>> Handle(GetRecentVotesQuery request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var q = _db.VotePolls
            .Include(v => v.Options)
            .Include(v => v.Ballots)
            .Include(v => v.EligibleUsers)
            .Where(v => v.Deadline < now);

        q = GetActiveVotesQueryHandler.FilterByEligibility(q, _user);

        var items = await q
            .OrderByDescending(v => v.Deadline)
            .Take(50)
            .ToListAsync(ct);

        return items.Select(GetActiveVotesQueryHandler.MapSummary).ToList();
    }
}
