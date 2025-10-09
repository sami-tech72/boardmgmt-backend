// Application/Calendars/Queries/GetCalendarRangeFromDbHandler.cs
using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Calendars.Queries;

public sealed class GetCalendarRangeFromDbHandler
  : IRequestHandler<GetCalendarRangeFromDbQuery, IReadOnlyList<CalendarEventDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;

    public GetCalendarRangeFromDbHandler(IAppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<CalendarEventDto>> Handle(GetCalendarRangeFromDbQuery request, CancellationToken ct)
    {
        // If not signed in, return empty (or throw, depending on your policy)
        if (!_current.IsAuthenticated)
            return Array.Empty<CalendarEventDto>();

        var userId = _current.UserId;                 // Identity user id (string)
        var emailLower = _current.Email?.Trim().ToLower(); // EF-translatable compare

        // Base overlap filter:
        // Overlap if (End > rangeStart) AND (Start < rangeEnd)
        IQueryable<Meeting> query = _db.Set<Meeting>()
            .AsNoTracking()
            .Where(m =>
                (m.EndAt ?? m.ScheduledAt.AddHours(1)) > request.StartUtc &&
                m.ScheduledAt < request.EndUtc
            );

        
        query = query.Where(m =>
            m.Attendees.Any(a =>
                (!string.IsNullOrEmpty(a.UserId) && a.UserId == userId) ||
                (!string.IsNullOrEmpty(a.Email) && emailLower != null && a.Email!.ToLower() == emailLower)
            )
        );
        
        // Order by scalar columns first (server-side), then project
        return await query
            .OrderBy(m => m.ScheduledAt)
            .ThenBy(m => m.EndAt ?? m.ScheduledAt.AddHours(1))
            .Select(m => new CalendarEventDto(
                m.Id.ToString(),
                m.Title,
                m.ScheduledAt,
                m.EndAt ?? m.ScheduledAt.AddHours(1),
                m.OnlineJoinUrl,
                // Avoid IsNullOrWhiteSpace to keep it SQL-translatable
                (m.ExternalCalendar != null && m.ExternalCalendar != "") ? m.ExternalCalendar : null
            ))
            .ToListAsync(ct);
    }
}
