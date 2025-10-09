// Application/Meetings/Commands/CreateMeetingHandler.cs
using System;
using System.Linq;
using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Common.Options;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Calendars;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;


namespace BoardMgmt.Application.Meetings.Commands;


public class CreateMeetingHandler : IRequestHandler<CreateMeetingCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IIdentityUserReader _users;
    private readonly ICalendarServiceSelector _calSelector;
    private readonly AppOptions _app;


    public CreateMeetingHandler(
        IAppDbContext db,
        IIdentityUserReader users,
        ICalendarServiceSelector calSelector,
        IOptions<AppOptions> appOptions)
    {
        _db = db;
        _users = users;
        _calSelector = calSelector;
        _app = appOptions.Value ?? new AppOptions();
    }


    public async Task<Guid> Handle(CreateMeetingCommand request, CancellationToken ct)
    {
        if (!CalendarProviders.IsSupported(request.Provider))
            throw new ArgumentOutOfRangeException("provider", $"Unknown calendar provider: {request.Provider}");


        var hostIdentity = ResolveHostIdentity(request.Provider, request.HostIdentity);

        var entity = new Meeting
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim(),
            Type = request.Type,
            ScheduledAt = request.ScheduledAt,
            EndAt = request.EndAt,
            Location = string.IsNullOrWhiteSpace(request.Location) ? "TBD" : request.Location.Trim(),
            Status = MeetingStatus.Scheduled,
            ExternalCalendar = request.Provider, // "Microsoft365" or "Zoom"
            ExternalCalendarMailbox = hostIdentity, // M365 mailbox or Zoom host email (optional)
            HostIdentity = hostIdentity
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
        var svc = _calSelector.For(request.Provider);
        var (eventId, joinUrl) = await svc.CreateEventAsync(entity, ct);
        entity.ExternalEventId = eventId;
        entity.OnlineJoinUrl = joinUrl;


        _db.Meetings.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    private string? ResolveHostIdentity(string provider, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
            return requested.Trim();

        if (string.Equals(provider, CalendarProviders.Microsoft365, StringComparison.Ordinal))
            return _app.MailboxAddress;

        return null;
    }
}
