// Infrastructure/Calendars/CalendarServiceSelector.cs
using System.Collections.Concurrent;
using BoardMgmt.Application.Calendars;
using BoardMgmt.Domain.Calendars;


namespace BoardMgmt.Infrastructure.Calendars;


public sealed class CalendarServiceSelector : ICalendarServiceSelector
{
    private readonly ConcurrentDictionary<string, ICalendarService> _map;


    public CalendarServiceSelector(IEnumerable<KeyValuePair<string, ICalendarService>> registrations)
    {
        _map = new ConcurrentDictionary<string, ICalendarService>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in registrations)
            _map[kv.Key] = kv.Value;
    }


    public ICalendarService For(string provider)
    {
        if (!CalendarProviders.IsSupported(provider))
            throw new ArgumentOutOfRangeException(nameof(provider), $"Unknown calendar provider: '{provider}'.");


        if (_map.TryGetValue(provider, out var svc))
            return svc;


        throw new InvalidOperationException($"No service registered for provider '{provider}'.");
    }
}