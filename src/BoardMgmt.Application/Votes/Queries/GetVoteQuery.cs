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
            .Include(x => x.Meeting)
                .ThenInclude(m => m.Attendees)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (v is null) return null;

        var options = v.Options
            .OrderBy(o => o.Order)
            .Select(o => new VoteOptionDto(
                o.Id, o.Text, o.Order, v.Ballots.Count(b => b.OptionId == o.Id)))
            .ToArray();

        var total = v.Ballots.Count;
        var yes = v.Type == VoteType.MultipleChoice ? 0 : v.Ballots.Count(b => b.Choice == VoteChoice.Yes);
        var no = v.Type == VoteType.MultipleChoice ? 0 : v.Ballots.Count(b => b.Choice == VoteChoice.No);
        var ab = v.Type == VoteType.MultipleChoice ? 0 : v.Ballots.Count(b => b.Choice == VoteChoice.Abstain);

        var results = new VoteResultsDto(total, yes, no, ab, options);

        var now = DateTimeOffset.UtcNow;
        var isOpen = v.IsOpen(now);

        var uid = user.UserId;
        var isAuthed = user.IsAuthenticated && !string.IsNullOrEmpty(uid);
        var authedUserId = isAuthed ? uid : null;

        bool eligible = v.Eligibility switch
        {
            VoteEligibility.Public => true,
            VoteEligibility.SpecificUsers =>
                authedUserId is not null && v.EligibleUsers.Any(e => e.UserId == authedUserId),
            VoteEligibility.MeetingAttendees =>
                authedUserId is not null && v.MeetingId != null &&
                (v.Meeting?.Attendees?.Any(a => a.UserId == authedUserId) ?? false),
            _ => false
        };

        var alreadyVoted = authedUserId is not null && v.Ballots.Any(b => b.UserId == authedUserId);

        // Build individual votes WITHOUT display names (Application layer stays pure)
        IReadOnlyList<IndividualVoteDto>? individualVotes = null;

        if (!v.Anonymous && v.Ballots.Count > 0)
        {
            string LabelFor(VoteBallot b) =>
                b.Choice switch
                {
                    VoteChoice.Yes => "Yes",
                    VoteChoice.No => "No",
                    VoteChoice.Abstain => "Abstain",
                    _ => b.OptionId.HasValue
                        ? options.FirstOrDefault(o => o.Id == b.OptionId.Value)?.Text ?? "—"
                        : "—"
                };

            string? OptionTextFor(VoteBallot b) =>
                b.OptionId.HasValue
                    ? options.FirstOrDefault(o => o.Id == b.OptionId.Value)?.Text
                    : null;

            individualVotes = v.Ballots
                .OrderByDescending(b => b.VotedAt)
                .Select(b => new IndividualVoteDto(
                    b.UserId,
                    null,                     // DisplayName filled in WebApi
                    b.Choice.HasValue ? LabelFor(b) : null,
                    OptionTextFor(b),
                    b.VotedAt))
                .ToList();
        }

        return new VoteDetailDto(
            v.Id, v.MeetingId, v.AgendaItemId,
            v.Title, v.Description, v.Type,
            v.AllowAbstain, v.Anonymous,
            v.CreatedAt, v.Deadline, v.Eligibility,
            options,
            results,
            CanVote: isOpen && eligible && !alreadyVoted,
            AlreadyVoted: alreadyVoted,
            IndividualVotes: individualVotes
        );
    }
}
