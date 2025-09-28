using BoardMgmt.Domain.Entities;

namespace BoardMgmt.Application.Calendars;

public interface ICalendarService
{
    Task<(string eventId, string? joinUrl)> CreateEventAsync(Meeting meeting, CancellationToken ct = default);
    Task<(bool ok, string? joinUrl)> UpdateEventAsync(Meeting meeting, CancellationToken ct = default);

    // ✅ add this:
    Task CancelEventAsync(string eventId, CancellationToken ct = default);

    Task<IReadOnlyList<CalendarEventDto>> ListUpcomingAsync(int take = 20, CancellationToken ct = default);
}

public sealed record CalendarEventDto(
    string Id,
    string Subject,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? JoinUrl
);
