using BoardMgmt.Domain.Entities;

namespace BoardMgmt.Application.Calendars;

public interface ICalendarService
{
    Task<(string eventId, string? joinUrl, string? onlineMeetingId)> CreateEventAsync(Meeting meeting, CancellationToken ct = default);
    Task<(bool ok, string? joinUrl, string? onlineMeetingId)> UpdateEventAsync(Meeting meeting, CancellationToken ct = default);

    Task CancelEventAsync(string eventId, CancellationToken ct = default);

    Task<IReadOnlyList<CalendarEventDto>> ListUpcomingAsync(int take = 20, CancellationToken ct = default);

    // NEW: list events within a window (UTC)
    Task<IReadOnlyList<CalendarEventDto>> ListRangeAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default);
}

public sealed record CalendarEventDto(
    string Id,
    string Subject,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? JoinUrl,
    string? Provider = "Microsoft365" // default string; services should override with actual provider
);


