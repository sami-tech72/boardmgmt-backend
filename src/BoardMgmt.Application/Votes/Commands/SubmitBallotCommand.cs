// BoardMgmt.Application/Votes/Commands/SubmitBallotCommand.cs
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Votes.DTOs;
using BoardMgmt.Application.Votes.Queries;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record SubmitBallotCommand(Guid VoteId, VoteChoice? Choice, Guid? OptionId)
    : IRequest<VoteSummaryDto>;

public sealed class SubmitBallotCommandHandler
    : IRequestHandler<SubmitBallotCommand, VoteSummaryDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _user;

    public SubmitBallotCommandHandler(IAppDbContext db, ICurrentUser user)
    { _db = db; _user = user; }

    public async Task<VoteSummaryDto> Handle(SubmitBallotCommand r, CancellationToken ct)
    {
        if (!_user.IsAuthenticated) throw new UnauthorizedAccessException();

        var v = await _db.VotePolls
            .Include(x => x.Options)
            .Include(x => x.Ballots) // needed to find/update the user's ballot + recompute results
            .Include(x => x.EligibleUsers)
            .Include(x => x.Meeting)!.ThenInclude(m => m.Attendees)
            .FirstOrDefaultAsync(x => x.Id == r.VoteId, ct)
            ?? throw new KeyNotFoundException("Vote not found.");

        if (!v.IsOpen(DateTimeOffset.UtcNow))
            throw new InvalidOperationException("Voting closed.");

        // Eligibility
        var eligible = v.Eligibility switch
        {
            VoteEligibility.Public => true,
            VoteEligibility.SpecificUsers => v.EligibleUsers.Any(e => e.UserId == _user.UserId),
            VoteEligibility.MeetingAttendees => v.MeetingId != null && v.Meeting!.Attendees.Any(a => a.UserId == _user.UserId),
            _ => false
        };
        if (!eligible) throw new UnauthorizedAccessException("Not eligible to vote.");

        // Validate payload for the poll type
        if (v.Type == VoteType.MultipleChoice)
        {
            if (r.OptionId is null || !v.Options.Any(o => o.Id == r.OptionId.Value))
                throw new ArgumentException("Invalid option.");
        }
        else
        {
            if (r.Choice is null) throw new ArgumentException("Choice required.");
            if (r.Choice == VoteChoice.Abstain && !v.AllowAbstain)
                throw new ArgumentException("Abstain not allowed.");
        }

        // ✅ Upsert the user's ballot (create if none, otherwise UPDATE to allow re-voting)
        var ballot = v.Ballots.FirstOrDefault(b => b.UserId == _user.UserId);
        if (ballot is null)
        {
            ballot = new VoteBallot { VoteId = v.Id, UserId = _user.UserId! };
            _db.VoteBallots.Add(ballot);
        }

        ballot.Choice = (v.Type == VoteType.MultipleChoice) ? null : r.Choice;
        ballot.OptionId = (v.Type == VoteType.MultipleChoice) ? r.OptionId : null;
        ballot.VotedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Reload light for fresh counts & user's current selection
        var refreshed = await _db.VotePolls
            .AsNoTracking()
            .Include(x => x.Options)
            .Include(x => x.Ballots)
            .FirstAsync(x => x.Id == v.Id, ct);

        // Returns VoteSummaryDto with results + alreadyVoted + myChoice/myOptionId
        return GetActiveVotesQueryHandler.MapSummary(refreshed, _user);
    }
}
