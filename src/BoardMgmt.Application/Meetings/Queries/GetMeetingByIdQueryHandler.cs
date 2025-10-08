using BoardMgmt.Application.Meetings.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Meetings.Queries;

public sealed class GetMeetingByIdQueryHandler : IRequestHandler<GetMeetingByIdQuery, MeetingDto?>
{
    private readonly DbContext _db;
    public GetMeetingByIdQueryHandler(DbContext db) => _db = db;

    public async Task<MeetingDto?> Handle(GetMeetingByIdQuery request, CancellationToken ct)
    {
        var meeting = await _db.Set<Meeting>()
            .AsNoTracking()
            .Include(m => m.AgendaItems)
            .Include(m => m.Attendees)
            .FirstOrDefaultAsync(m => m.Id == request.Id, ct);

        if (meeting == null) return null;

        DateTimeOffset scheduledAtUtc = meeting.ScheduledAt;
        if (scheduledAtUtc.Offset != TimeSpan.Zero) scheduledAtUtc = scheduledAtUtc.ToUniversalTime();

        DateTimeOffset? endAtUtc = meeting.EndAt;
        if (endAtUtc.HasValue && endAtUtc.Value.Offset != TimeSpan.Zero) endAtUtc = endAtUtc.Value.ToUniversalTime();

        var agendaItems = meeting.AgendaItems
            .OrderBy(ai => ai.Order)
            .Select(ai => new AgendaItemDto(ai.Id, ai.Title, ai.Description, ai.Order))
            .ToList();

        var attendees = meeting.Attendees
            .OrderBy(a => a.Name)
            .Select(a => new AttendeeDto(
                a.Id,
                a.Name,
                a.Email,
                a.Role,
                a.UserId,
                (a.RowVersion != null && a.RowVersion.Length > 0)
                            ? Convert.ToBase64String(a.RowVersion)
                            : string.Empty
                    ))
                 .ToList();

        var dto = new MeetingDto(
             meeting.Id,
             meeting.Title,
             meeting.Description,
             meeting.Type,
             scheduledAtUtc,
             endAtUtc,
             meeting.Location,
             meeting.Status,
             meeting.Attendees.Count,
             agendaItems,
             attendees,
             meeting.OnlineJoinUrl,
             meeting.ExternalCalendar,
             meeting.HostIdentity
         );

        return dto;
    }
}
