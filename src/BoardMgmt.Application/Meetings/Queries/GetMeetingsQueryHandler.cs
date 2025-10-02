// Application/Meetings/Queries/GetMeetingsQueryHandler.cs
using BoardMgmt.Application.Common.Interfaces;
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
    private readonly ICurrentUser _current;

    public GetMeetingsQueryHandler(DbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<MeetingDto>> Handle(GetMeetingsQuery request, CancellationToken ct)
    {
        // If not authenticated, nothing to show
        if (!_current.IsAuthenticated)
            return Array.Empty<MeetingDto>();

        var userId = _current.UserId;                 // Identity user id (string)
        var emailLower = _current.Email?.Trim().ToLower(); // EF-translatable case-insensitive compare

      

        // Start as IQueryable so we can conditionally add Where before Includes
        IQueryable<Meeting> query = _db.Set<Meeting>().AsNoTracking();

     
            // Non-admins: restrict to meetings where they are an attendee (by UserId or Email)
            query = query.Where(m =>
                m.Attendees.Any(a =>
                    (!string.IsNullOrEmpty(a.UserId) && a.UserId == userId) ||
                    (!string.IsNullOrEmpty(a.Email) && emailLower != null && a.Email!.ToLower() == emailLower)
                )
            );
       

        // Add includes after filtering
        query = query
            .Include(m => m.AgendaItems)
            .Include(m => m.Attendees);

        // Project to DTOs
        return await query
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
                            ? System.Convert.ToBase64String(a.RowVersion)
                            : string.Empty
                    ))
                    .ToList(),
                m.OnlineJoinUrl,
                m.ExternalCalendar ?? "Zoom" // use stored provider if present
            ))
            .ToListAsync(ct);
    }
}
