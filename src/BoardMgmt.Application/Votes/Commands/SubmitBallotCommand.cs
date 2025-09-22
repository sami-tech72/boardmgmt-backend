using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;


namespace BoardMgmt.Application.Votes.Commands;

public sealed record SubmitBallotCommand(Guid VoteId, VoteChoice? Choice, Guid? OptionId) : IRequest;

public sealed class SubmitBallotCommandHandler : IRequestHandler<SubmitBallotCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _user;

    public SubmitBallotCommandHandler(IAppDbContext db, ICurrentUser user)
    { _db = db; _user = user; }

    public async Task Handle(SubmitBallotCommand r, CancellationToken ct)
    {
        if (!_user.IsAuthenticated) throw new UnauthorizedAccessException();

        var v = await _db.VotePolls
            .Include(x => x.Options)
            .Include(x => x.EligibleUsers)
            .Include(x => x.Meeting)!.ThenInclude(m => m.Attendees)
            .FirstOrDefaultAsync(x => x.Id == r.VoteId, ct);

        if (v is null) throw new KeyNotFoundException("Vote not found.");
        if (!v.IsOpen(DateTimeOffset.UtcNow)) throw new InvalidOperationException("Voting closed.");

        // Eligibility check
        var eligible = v.Eligibility switch
        {
            VoteEligibility.Public => true,
            VoteEligibility.SpecificUsers => v.EligibleUsers.Any(e => e.UserId == _user.UserId),
            VoteEligibility.MeetingAttendees => v.MeetingId != null && v.Meeting!.Attendees.Any(a => a.UserId == _user.UserId),
            _ => false
        };
        if (!eligible) throw new UnauthorizedAccessException("Not eligible to vote.");

        // Validate payload
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

        // Upsert user's ballot
        var existing = await _db.VoteBallots.FirstOrDefaultAsync(b => b.VoteId == v.Id && b.UserId == _user.UserId, ct);
        if (existing is null)
        {
            existing = new VoteBallot { VoteId = v.Id, UserId = _user.UserId! };
            _db.VoteBallots.Add(existing);
        }

        existing.Choice = (v.Type == VoteType.MultipleChoice) ? null : r.Choice;
        existing.OptionId = (v.Type == VoteType.MultipleChoice) ? r.OptionId : null;
        existing.VotedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
