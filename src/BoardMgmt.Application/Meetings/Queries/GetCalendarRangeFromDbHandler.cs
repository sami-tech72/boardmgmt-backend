// Application/Calendars/Queries/GetCalendarRangeFromDbHandler.cs
using BoardMgmt.Application.Calendars;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Calendars.Queries;

public sealed class GetCalendarRangeFromDbHandler
  : IRequestHandler<GetCalendarRangeFromDbQuery, IReadOnlyList<CalendarEventDto>>
{
    private readonly DbContext _db;
    public GetCalendarRangeFromDbHandler(DbContext db) => _db = db;

    public async Task<IReadOnlyList<CalendarEventDto>> Handle(GetCalendarRangeFromDbQuery request, CancellationToken ct)
    {
        // Helper inlined so EF can translate:
        // computedEnd = m.EndAt ?? m.ScheduledAt.AddHours(1)

        return await _db.Set<Meeting>()
            .AsNoTracking()
            .Where(m =>
                // Overlap: (End > rangeStart) AND (Start < rangeEnd)
                (m.EndAt ?? m.ScheduledAt.AddHours(1)) > request.StartUtc &&
                m.ScheduledAt < request.EndUtc)
            // Order BEFORE projecting to DTO so SQL orders by a scalar column
            .OrderBy(m => m.ScheduledAt)
            .ThenBy(m => m.EndAt ?? m.ScheduledAt.AddHours(1))
            .Select(m => new CalendarEventDto(
                m.Id.ToString(),
                m.Title,
                m.ScheduledAt,
                m.EndAt ?? m.ScheduledAt.AddHours(1),
                m.OnlineJoinUrl,
                // avoid IsNullOrWhiteSpace in SQL translation
                (m.ExternalCalendar != null && m.ExternalCalendar != "") ? m.ExternalCalendar : null
            ))
            .ToListAsync(ct);
    }
}
