using System.Linq;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Options;
using BoardMgmt.Application.Calendars;
using BoardMgmt.Domain.Entities;

namespace BoardMgmt.Infrastructure.Calendars;

public sealed class Microsoft365CalendarService : ICalendarService
{
    private readonly GraphServiceClient _graph;
    private readonly GraphOptions _opts;

    public Microsoft365CalendarService(GraphServiceClient graph, IOptions<GraphOptions> opts)
    {
        _graph = graph;
        _opts = opts.Value;
    }

    public async Task<(string eventId, string? joinUrl)> CreateEventAsync(Meeting m, CancellationToken ct = default)
    {
        var end = m.EndAt ?? m.ScheduledAt.AddHours(1);
        var ev = new Event
        {
            Subject = m.Title,
            Body = new ItemBody { ContentType = BodyType.Html, Content = m.Description ?? string.Empty },
            Start = new DateTimeTimeZone { DateTime = m.ScheduledAt.UtcDateTime.ToString("o"), TimeZone = "UTC" },
            End = new DateTimeTimeZone { DateTime = end.UtcDateTime.ToString("o"), TimeZone = "UTC" },
            Location = new Location { DisplayName = string.IsNullOrWhiteSpace(m.Location) ? "TBD" : m.Location },
            IsOnlineMeeting = true,
            OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness,
            Attendees = m.Attendees.Select(a => new Attendee
            {
                Type = a.IsRequired ? AttendeeType.Required : AttendeeType.Optional,
                EmailAddress = new EmailAddress { Address = a.Email ?? string.Empty, Name = a.Name }
            }).ToList()
        };

        var mailbox = string.IsNullOrWhiteSpace(m.ExternalCalendarMailbox)
            ? _opts.MailboxAddress
            : m.ExternalCalendarMailbox;

        var created = await _graph.Users[mailbox].Events.PostAsync(ev, cancellationToken: ct);
        return (created?.Id ?? throw new InvalidOperationException("Graph didn't return Event Id."),
                created?.OnlineMeeting?.JoinUrl);
    }

    public async Task<(bool ok, string? joinUrl)> UpdateEventAsync(Meeting m, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(m.ExternalEventId)) return (false, null);

        var end = m.EndAt ?? m.ScheduledAt.AddHours(1);
        var patch = new Event
        {
            Subject = m.Title,
            Body = new ItemBody { ContentType = BodyType.Html, Content = m.Description ?? string.Empty },
            Start = new DateTimeTimeZone { DateTime = m.ScheduledAt.UtcDateTime.ToString("o"), TimeZone = "UTC" },
            End = new DateTimeTimeZone { DateTime = end.UtcDateTime.ToString("o"), TimeZone = "UTC" },
            Location = new Location { DisplayName = string.IsNullOrWhiteSpace(m.Location) ? "TBD" : m.Location },
            Attendees = m.Attendees.Select(a => new Attendee
            {
                Type = a.IsRequired ? AttendeeType.Required : AttendeeType.Optional,
                EmailAddress = new EmailAddress { Address = a.Email ?? string.Empty, Name = a.Name }
            }).ToList()
        };

        var mailbox = string.IsNullOrWhiteSpace(m.ExternalCalendarMailbox)
            ? _opts.MailboxAddress
            : m.ExternalCalendarMailbox;

        await _graph.Users[mailbox].Events[m.ExternalEventId]
            .PatchAsync(
                patch,
                requestConfiguration => requestConfiguration.Headers.Add("If-Match", "*"),
                cancellationToken: ct);

        var refreshed = await _graph.Users[mailbox].Events[m.ExternalEventId]
            .GetAsync(cancellationToken: ct);

        return (true, refreshed?.OnlineMeeting?.JoinUrl);
    }

    public async Task CancelEventAsync(string eventId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return;
        await _graph.Users[_opts.MailboxAddress].Events[eventId].DeleteAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> ListUpcomingAsync(int take = 20, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var resp = await _graph.Users[_opts.MailboxAddress].CalendarView.GetAsync(cfg =>
        {
            cfg.QueryParameters.StartDateTime = now.ToString("o");
            cfg.QueryParameters.EndDateTime = now.AddDays(30).ToString("o");
            cfg.QueryParameters.Top = take;
            cfg.QueryParameters.Orderby = new[] { "start/dateTime" };
        }, ct);

        var list = resp?.Value ?? new List<Event>();
        return list.Select(e => new CalendarEventDto(
            e.Id!,
            e.Subject ?? "(no subject)",
            ParseUtc(e.Start),
            ParseUtc(e.End),
            e.OnlineMeeting?.JoinUrl,
            "Microsoft365"
        )).ToList();
    }

    public async Task<IReadOnlyList<CalendarEventDto>> ListRangeAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default)
    {
        var resp = await _graph.Users[_opts.MailboxAddress].CalendarView.GetAsync(cfg =>
        {
            cfg.QueryParameters.StartDateTime = startUtc.ToString("o");
            cfg.QueryParameters.EndDateTime = endUtc.ToString("o");
            cfg.QueryParameters.Orderby = new[] { "start/dateTime" };
            cfg.QueryParameters.Top = 100;
        }, ct);

        var list = resp?.Value ?? new List<Event>();
        return list.Select(e => new CalendarEventDto(
            e.Id!,
            e.Subject ?? "(no subject)",
            ParseUtc(e.Start),
            ParseUtc(e.End),
            e.OnlineMeeting?.JoinUrl,
            "Microsoft365"
        )).ToList();
    }

    private static DateTimeOffset ParseUtc(DateTimeTimeZone? z)
    {
        if (z?.DateTime is null) return DateTimeOffset.MinValue;
        return DateTimeOffset.Parse(z.DateTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }
}
