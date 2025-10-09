using System;
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
                EmailAddress = string.IsNullOrWhiteSpace(a.Email)
                    ? null
                    : new EmailAddress { Address = a.Email, Name = a.Name }
            })
            .Where(a => a.EmailAddress is not null)
            .ToList()
        };

        var mailbox = string.IsNullOrWhiteSpace(m.ExternalCalendarMailbox)
            ? _opts.MailboxAddress
            : m.ExternalCalendarMailbox;

        var created = await _graph.Users[mailbox].Events.PostAsync(ev, cancellationToken: ct)
                      ?? throw new InvalidOperationException("Graph didn't return Event.");

        var eventId = created.Id
                      ?? throw new InvalidOperationException("Graph didn't return Event Id.");

        var joinUrl = await FetchJoinUrlAsync(mailbox, eventId, ct);
        if (string.IsNullOrWhiteSpace(joinUrl))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
            joinUrl = await FetchJoinUrlAsync(mailbox, eventId, ct);
        }
        return (eventId, joinUrl);
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
                EmailAddress = string.IsNullOrWhiteSpace(a.Email)
                    ? null
                    : new EmailAddress { Address = a.Email, Name = a.Name }
            })
            .Where(a => a.EmailAddress is not null)
            .ToList()
        };

        var mailbox = string.IsNullOrWhiteSpace(m.ExternalCalendarMailbox)
            ? _opts.MailboxAddress
            : m.ExternalCalendarMailbox;

        await _graph.Users[mailbox].Events[m.ExternalEventId]
            .PatchAsync(
                patch,
                requestConfiguration => requestConfiguration.Headers.Add("If-Match", "*"),
                cancellationToken: ct);

        var joinUrl = await FetchJoinUrlAsync(mailbox, m.ExternalEventId, ct);
        return (true, joinUrl);
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
            cfg.QueryParameters.Select = new[] { "id", "subject", "start", "end", "onlineMeeting" };
            cfg.QueryParameters.Expand = new[] { "onlineMeeting" };
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
            cfg.QueryParameters.Select = new[] { "id", "subject", "start", "end", "onlineMeeting" };
            cfg.QueryParameters.Expand = new[] { "onlineMeeting" };
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

    private async Task<string?> FetchJoinUrlAsync(string mailbox, string eventId, CancellationToken ct)
    {
        var ev = await _graph.Users[mailbox].Events[eventId].GetAsync(cfg =>
        {
            cfg.QueryParameters.Select = new[] { "onlineMeeting" };
            cfg.QueryParameters.Expand = new[] { "onlineMeeting" };
        }, ct);

        return ev?.OnlineMeeting?.JoinUrl;
    }
}
