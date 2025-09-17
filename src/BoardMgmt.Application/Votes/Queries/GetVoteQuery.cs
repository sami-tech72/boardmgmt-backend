using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Votes.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Votes.Queries;

public sealed record GetVoteQuery(Guid Id) : IRequest<VoteDetailDto?>;

public sealed class GetVoteQueryHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetVoteQuery, VoteDetailDto?>
{
    public async Task<VoteDetailDto?> Handle(GetVoteQuery request, CancellationToken ct)
    {
        var v = await db.VotePolls
            .Include(x => x.Options)
            .Include(x => x.Ballots)
            .Include(x => x.EligibleUsers)
            .Include(x => x.Meeting)!.ThenInclude(m => m.Attendees) // safe even if MeetingId is null
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (v is null) return null;

        var options = v.Options
            .OrderBy(o => o.Order)
            .Select(o => new VoteOptionDto(
                o.Id, o.Text, o.Order, v.Ballots.Count(b => b.OptionId == o.Id)))
            .ToArray();

        // non-MC totals
        var total = v.Ballots.Count;
        var yes = v.Type == VoteType.MultipleChoice ? 0 : v.Ballots.Count(b => b.Choice == VoteChoice.Yes);
        var no = v.Type == VoteType.MultipleChoice ? 0 : v.Ballots.Count(b => b.Choice == VoteChoice.No);
        var ab = v.Type == VoteType.MultipleChoice ? 0 : v.Ballots.Count(b => b.Choice == VoteChoice.Abstain);

        var results = new VoteResultsDto(total, yes, no, ab, options);

        var now = DateTimeOffset.UtcNow;
        var isOpen = v.IsOpen(now);

        // Can the current user vote?
        var uid = user.UserId;
        var isAuthed = user.IsAuthenticated && !string.IsNullOrEmpty(uid);

        bool eligible = v.Eligibility switch
        {
            VoteEligibility.Public => true,
            VoteEligibility.SpecificUsers => isAuthed && v.EligibleUsers.Any(e => e.UserId == uid),
            VoteEligibility.MeetingAttendees => isAuthed && v.MeetingId != null &&
                                                v.Meeting!.Attendees.Any(a => a.UserId == uid),
            _ => false
        };

        var alreadyVoted = isAuthed && v.Ballots.Any(b => b.UserId == uid);

        return new VoteDetailDto(
            v.Id, v.MeetingId, v.AgendaItemId,
            v.Title, v.Description, v.Type,
            v.AllowAbstain, v.Anonymous,
            v.CreatedAt, v.Deadline, v.Eligibility,
            options,
            results,
            CanVote: isOpen && eligible && !alreadyVoted,
            AlreadyVoted: alreadyVoted
        );
    }
}
