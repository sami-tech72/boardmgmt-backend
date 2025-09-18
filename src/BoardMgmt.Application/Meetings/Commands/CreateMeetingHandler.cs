using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Meetings.Commands;

public class CreateMeetingHandler : IRequestHandler<CreateMeetingCommand, Guid>
{
    private readonly DbContext _db;
    private readonly IIdentityUserReader _users;

    public CreateMeetingHandler(DbContext db, IIdentityUserReader users)
    {
        _db = db;
        _users = users;
    }

    public async Task<Guid> Handle(CreateMeetingCommand request, CancellationToken ct)
    {
        var entity = new Meeting
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim(),
            Type = request.Type,
            ScheduledAt = request.ScheduledAt,
            EndAt = request.EndAt,
            Location = string.IsNullOrWhiteSpace(request.Location) ? "TBD" : request.Location.Trim(),
            Status = MeetingStatus.Scheduled
        };

        if (request.attendeeUserIds is { Count: > 0 })
        {
            var users = await _users.GetByIdsAsync(request.attendeeUserIds, ct);

            foreach (var u in users)
            {
                var name = string.IsNullOrWhiteSpace(u.DisplayName) ? (u.Email ?? "Unknown") : u.DisplayName!;
                entity.Attendees.Add(new MeetingAttendee
                {
                    UserId = u.Id,
                    Name = name,
                    Email = u.Email,
                    IsRequired = true,
                    IsConfirmed = false
                });
            }
        }
        else if (request.Attendees is { Count: > 0 })
        {
            foreach (var raw in request.Attendees.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var full = raw.Trim();
                string name = full;
                string? role = null;

                var open = full.IndexOf('(');
                var close = full.IndexOf(')');
                if (open > 0 && close > open)
                {
                    name = full[..open].Trim();
                    role = full[(open + 1)..close].Trim();
                }

                entity.Attendees.Add(new MeetingAttendee
                {
                    Name = name,
                    Role = role,
                    IsRequired = true,
                    IsConfirmed = false
                });
            }
        }

        _db.Set<Meeting>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }
}
