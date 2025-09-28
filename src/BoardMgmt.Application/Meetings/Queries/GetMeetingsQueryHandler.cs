using BoardMgmt.Application.Meetings.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BoardMgmt.Application.Meetings.Queries;

public class GetMeetingsQueryHandler : IRequestHandler<GetMeetingsQuery, IReadOnlyList<MeetingDto>>
{
    private readonly DbContext _db;
    public GetMeetingsQueryHandler(DbContext db) => _db = db;

    public async Task<IReadOnlyList<MeetingDto>> Handle(GetMeetingsQuery request, CancellationToken ct)
    {
        return await _db.Set<Meeting>()
            .AsNoTracking()
            .Include(m => m.AgendaItems)
            .Include(m => m.Attendees)
            .OrderBy(m => m.ScheduledAt)
             .Select(m => new MeetingDto(
                m.Id,
                m.Title,
                m.Description,
                m.Type,
                m.ScheduledAt,
                m.EndAt,
                m.Location,
                m.Status,
                m.Attendees.Count,
                m.AgendaItems
                    .OrderBy(ai => ai.Order)
                    .Select(ai => new AgendaItemDto(ai.Id, ai.Title, ai.Description, ai.Order))
                    .ToList(),
               m.Attendees
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
                 .ToList(),
               m.OnlineJoinUrl
            ))
            .ToListAsync(ct);
    }
}
