// BoardMgmt.Application/Votes/Queries/GetActiveVotesQuery.cs
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Votes.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Votes.Queries;

public sealed record GetActiveVotesQuery : IRequest<IReadOnlyList<VoteSummaryDto>>;

public sealed class GetActiveVotesQueryHandler
    : IRequestHandler<GetActiveVotesQuery, IReadOnlyList<VoteSummaryDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _user;

    public GetActiveVotesQueryHandler(IAppDbContext db, ICurrentUser user)
    { _db = db; _user = user; }

    public async Task<IReadOnlyList<VoteSummaryDto>> Handle(GetActiveVotesQuery request, CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;

        var q = _db.VotePolls
            .AsNoTracking()
            .Include(v => v.Options)
            .Include(v => v.Ballots)
            .Include(v => v.EligibleUsers)
            .Include(v => v.Meeting)!.ThenInclude(m => m.Attendees)
            .Where(v => v.Deadline >= nowUtc);

        q = FilterByEligibility(q, _user);

        var items = await q.OrderBy(v => v.Deadline).ToListAsync(ct);
        return items.Select(v => MapSummary(v, _user)).ToList();
    }

    internal static IQueryable<VotePoll> FilterByEligibility(IQueryable<VotePoll> q, ICurrentUser user)
    {
        if (user.IsAuthenticated is false)
            return q.Where(v => v.Eligibility == VoteEligibility.Public);

        return q.Where(v =>
               v.Eligibility == VoteEligibility.Public
            || v.CreatedByUserId == user.UserId
            || (v.Eligibility == VoteEligibility.SpecificUsers
                && v.EligibleUsers.Any(e => e.UserId == user.UserId))
            || (v.Eligibility == VoteEligibility.MeetingAttendees
                && v.MeetingId != null
                && v.Meeting!.Attendees.Any(a => a.UserId == user.UserId))
        );
    }

    internal static VoteSummaryDto MapSummary(VotePoll v, ICurrentUser user)
    {
        var results = BuildResults(v);

        // compute current user's ballot (if any)
        var myBallot = user.IsAuthenticated
            ? v.Ballots.FirstOrDefault(b => b.UserId == user.UserId)
            : null;

        return new VoteSummaryDto(
            v.Id,
            v.Title,
            v.Description,
            v.Type,
            v.Deadline,
            v.IsOpen(DateTimeOffset.UtcNow),
            v.Eligibility,
            results,
            myBallot != null,                         // AlreadyVoted
            v.Type == VoteType.MultipleChoice ? null : myBallot?.Choice, // MyChoice
            v.Type == VoteType.MultipleChoice ? myBallot?.OptionId : null // MyOptionId
        );
    }

    internal static VoteResultsDto BuildResults(VotePoll v)
    {
        var total = v.Ballots.Count;
        int yes = 0, no = 0, abstain = 0;

        if (v.Type != VoteType.MultipleChoice)
        {
            yes = v.Ballots.Count(b => b.Choice == VoteChoice.Yes);
            no = v.Ballots.Count(b => b.Choice == VoteChoice.No);
            abstain = v.Ballots.Count(b => b.Choice == VoteChoice.Abstain);
        }

        var optCounts = v.Options
            .OrderBy(o => o.Order)
            .Select(o => new VoteOptionDto(
                o.Id,
                o.Text,
                o.Order,
                v.Ballots.Count(b => b.OptionId == o.Id)))
            .ToList();

        return new VoteResultsDto(total, yes, no, abstain, optCounts);
    }
}
