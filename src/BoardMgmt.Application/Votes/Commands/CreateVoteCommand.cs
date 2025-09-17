using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Votes.Commands;

public sealed record CreateVoteCommand(
    string Title,
    string? Description,
    VoteType Type,
    bool AllowAbstain,
    bool Anonymous,
    DateTimeOffset Deadline,
    VoteEligibility Eligibility,
    Guid? MeetingId,
    Guid? AgendaItemId,
    IReadOnlyList<string>? Options,
    IReadOnlyList<string>? SpecificUserIds
) : IRequest<Guid>;

public sealed class CreateVoteCommandHandler : IRequestHandler<CreateVoteCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _user;

    public CreateVoteCommandHandler(IAppDbContext db, ICurrentUser user)
    { _db = db; _user = user; }

    public async Task<Guid> Handle(CreateVoteCommand r, CancellationToken ct)
    {
        if (!_user.IsAuthenticated) throw new UnauthorizedAccessException();

        // If MeetingAttendees is chosen, a meeting is required.
        if (r.Eligibility == VoteEligibility.MeetingAttendees && r.MeetingId is null)
            throw new ArgumentException("MeetingAttendees eligibility requires a MeetingId.");

        var v = new VotePoll
        {
            Title = r.Title.Trim(),
            Description = r.Description?.Trim(),
            Type = r.Type,
            AllowAbstain = r.AllowAbstain,
            Anonymous = r.Anonymous,
            Deadline = r.Deadline,
            Eligibility = r.Eligibility,
            MeetingId = r.MeetingId,
            AgendaItemId = r.AgendaItemId,
            CreatedByUserId = _user.UserId ?? string.Empty,
        };

        // Multiple choice options
        if (v.Type == VoteType.MultipleChoice)
        {
            var opts = r.Options?.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).Distinct().ToList() ?? new();
            if (opts.Count < 2) throw new ArgumentException("Multiple choice needs at least 2 options.");
            v.Options = opts.Select((t, i) => new VoteOption { Text = t, Order = i }).ToList();
        }

        // Specific users eligibility — ensure creator is included
        if (v.Eligibility == VoteEligibility.SpecificUsers)
        {
            var ids = r.SpecificUserIds?
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id.Trim())
                        .Distinct()
                        .ToList() ?? new();

            var me = _user.UserId!;
            if (!ids.Contains(me)) ids.Add(me);

            if (ids.Count == 0)
                throw new ArgumentException("SpecificUsers eligibility requires at least one user.");

            v.EligibleUsers = ids.Select(id => new VoteEligibleUser { UserId = id }).ToList();
        }

        _db.VotePolls.Add(v);
        await _db.SaveChangesAsync(ct);
        return v.Id;
    }
}
