// Application/Meetings/Commands/CreateMeetingHandler.cs
using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Calendars;
using MediatR;
using Microsoft.EntityFrameworkCore;


namespace BoardMgmt.Application.Meetings.Commands;


public class CreateMeetingHandler : IRequestHandler<CreateMeetingCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IIdentityUserReader _users;
    private readonly ICalendarServiceSelector _calSelector;


    public CreateMeetingHandler(IAppDbContext db, IIdentityUserReader users, ICalendarServiceSelector calSelector)
    {
        _db = db;
        _users = users;
        _calSelector = calSelector;
    }


    public async Task<Guid> Handle(CreateMeetingCommand request, CancellationToken ct)
    {
        var provider = CalendarProviders.Normalize(request.Provider);

        if (!CalendarProviders.IsSupported(provider))
            throw new ArgumentOutOfRangeException(nameof(request.Provider), $"Unknown calendar provider: {request.Provider}");

        string? hostIdentity = string.IsNullOrWhiteSpace(request.HostIdentity)
            ? null
            : request.HostIdentity!.Trim();

        string? normalizedMailbox = hostIdentity;

        if (string.Equals(provider, CalendarProviders.Microsoft365, StringComparison.OrdinalIgnoreCase))
        {
            normalizedMailbox = MailboxIdentifier.Normalize(hostIdentity);
        }

        var entity = new Meeting
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim(),
            Type = request.Type,
            ScheduledAt = request.ScheduledAt,
            EndAt = request.EndAt,
            Location = string.IsNullOrWhiteSpace(request.Location) ? "TBD" : request.Location.Trim(),
            Status = MeetingStatus.Scheduled,
            ExternalCalendar = provider!, // "Microsoft365" or "Zoom"
            ExternalCalendarMailbox = normalizedMailbox,
            HostIdentity = normalizedMailbox ?? hostIdentity
        };


        // Attach attendees (same as before)
        if (request.AttendeeUserIds is { Count: > 0 })
        {
            var users = await _users.GetByIdsAsync(request.AttendeeUserIds, ct);
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
                string name = full; string? role = null;
                var open = full.IndexOf('('); var close = full.IndexOf(')');
                if (open > 0 && close > open) { name = full[..open].Trim(); role = full[(open + 1)..close].Trim(); }
                entity.Attendees.Add(new MeetingAttendee { Name = name, Role = role, IsRequired = true, IsConfirmed = false });
            }
        }


        // Create in chosen provider
        var svc = _calSelector.For(provider!);
        var (eventId, joinUrl) = await svc.CreateEventAsync(entity, ct);
        entity.ExternalEventId = eventId;
        entity.OnlineJoinUrl = joinUrl;


        _db.Meetings.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }
}