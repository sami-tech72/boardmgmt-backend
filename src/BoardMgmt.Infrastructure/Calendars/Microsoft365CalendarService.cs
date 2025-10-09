using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions; // ApiException (Graph v5 / Kiota)
using BoardMgmt.Application.Calendars;
using BoardMgmt.Domain.Entities;

namespace BoardMgmt.Infrastructure.Calendars;

public sealed class Microsoft365CalendarService : ICalendarService
{
    private readonly GraphServiceClient _graph;
    private readonly GraphOptions _opts;
    private readonly ILogger<Microsoft365CalendarService> _logger;

    public Microsoft365CalendarService(
        GraphServiceClient graph,
        IOptions<GraphOptions> opts,
        ILogger<Microsoft365CalendarService> logger)
    {
        _graph = graph;
        _opts = opts.Value;
        _logger = logger;
    }

    // ---------------- ICalendarService ----------------

    public async Task<(string eventId, string? joinUrl)> CreateEventAsync(Meeting m, CancellationToken ct = default)
    {
        var end = m.EndAt ?? m.ScheduledAt.AddHours(1);
        var mailbox = string.IsNullOrWhiteSpace(m.ExternalCalendarMailbox)
            ? _opts.MailboxAddress
            : m.ExternalCalendarMailbox;

        var ev = new Event
        {
            Subject = m.Title,
            Body = new ItemBody { ContentType = BodyType.Html, Content = m.Description ?? string.Empty },
            Start = new DateTimeTimeZone { DateTime = m.ScheduledAt.UtcDateTime.ToString("o"), TimeZone = "UTC" },
            End = new DateTimeTimeZone { DateTime = end.UtcDateTime.ToString("o"), TimeZone = "UTC" },
            Location = new Location { DisplayName = string.IsNullOrWhiteSpace(m.Location) ? "TBD" : m.Location },

            // Teams meeting
            IsOnlineMeeting = true,
            OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness,

            Attendees = m.Attendees.Select(a => new Attendee
            {
                Type = a.IsRequired ? AttendeeType.Required : AttendeeType.Optional,
                EmailAddress = new EmailAddress { Address = a.Email ?? string.Empty, Name = a.Name }
            }).ToList()
        };

        // Create
        var created = await RunGraph<Event?>(
            () => _graph.Users[mailbox].Events.PostAsync(ev, cancellationToken: ct),
            "POST /users/{mailbox}/events", ct);

        var id = created?.Id ?? throw new InvalidOperationException("Graph didn't return Event Id.");

        // Refetch with $select=onlineMeeting (short retry for consistency)
        var fetched = await GetEventWithOnlineMeetingAsync(mailbox!, id, ct);
        await EnsureTeamsMeetingDefaultsAsync(mailbox!, id, fetched, ct);
        var joinUrl = fetched?.OnlineMeeting?.JoinUrl;

        return (id, joinUrl);
    }

    public async Task<(bool ok, string? joinUrl)> UpdateEventAsync(Meeting m, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(m.ExternalEventId)) return (false, null);

        var end = m.EndAt ?? m.ScheduledAt.AddHours(1);
        var mailbox = string.IsNullOrWhiteSpace(m.ExternalCalendarMailbox)
            ? _opts.MailboxAddress
            : m.ExternalCalendarMailbox;

        var patch = new Event
        {
            Subject = m.Title,
            Body = new ItemBody { ContentType = BodyType.Html, Content = m.Description ?? string.Empty },
            Start = new DateTimeTimeZone { DateTime = m.ScheduledAt.UtcDateTime.ToString("o"), TimeZone = "UTC" },
            End = new DateTimeTimeZone { DateTime = end.UtcDateTime.ToString("o"), TimeZone = "UTC" },
            Location = new Location { DisplayName = string.IsNullOrWhiteSpace(m.Location) ? "TBD" : m.Location },

            // Ensure/keep Teams meeting
            IsOnlineMeeting = true,
            OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness,

            Attendees = m.Attendees.Select(a => new Attendee
            {
                Type = a.IsRequired ? AttendeeType.Required : AttendeeType.Optional,
                EmailAddress = new EmailAddress { Address = a.Email ?? string.Empty, Name = a.Name }
            }).ToList()
        };

        await RunGraph(
            () => _graph.Users[mailbox].Events[m.ExternalEventId].PatchAsync(
                patch,
                requestConfiguration => requestConfiguration.Headers.Add("If-Match", "*"),
                cancellationToken: ct),
            "PATCH /users/{mailbox}/events/{id}", ct);

        var refreshed = await GetEventWithOnlineMeetingAsync(mailbox!, m.ExternalEventId!, ct);
        await EnsureTeamsMeetingDefaultsAsync(mailbox!, m.ExternalEventId!, refreshed, ct);
        return (true, refreshed?.OnlineMeeting?.JoinUrl);
    }

    public async Task CancelEventAsync(string eventId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return;

        await RunGraph(
            () => _graph.Users[_opts.MailboxAddress].Events[eventId].DeleteAsync(cancellationToken: ct),
            "DELETE /users/{mailbox}/events/{id}", ct);
    }

    public async Task<IReadOnlyList<CalendarEventDto>> ListUpcomingAsync(int take = 20, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var resp = await RunGraph<EventCollectionResponse?>(
            () => _graph.Users[_opts.MailboxAddress].CalendarView.GetAsync(cfg =>
            {
                cfg.QueryParameters.StartDateTime = now.ToString("o");
                cfg.QueryParameters.EndDateTime = now.AddDays(30).ToString("o");
                cfg.QueryParameters.Top = take;
                cfg.QueryParameters.Orderby = new[] { "start/dateTime" };
                cfg.QueryParameters.Select = new[] { "id", "subject", "start", "end", "onlineMeeting" };
            }, ct),
            "GET /users/{mailbox}/calendarView?$select=onlineMeeting", ct);

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
        var resp = await RunGraph<EventCollectionResponse?>(
            () => _graph.Users[_opts.MailboxAddress].CalendarView.GetAsync(cfg =>
            {
                cfg.QueryParameters.StartDateTime = startUtc.ToString("o");
                cfg.QueryParameters.EndDateTime = endUtc.ToString("o");
                cfg.QueryParameters.Orderby = new[] { "start/dateTime" };
                cfg.QueryParameters.Top = 100;
                cfg.QueryParameters.Select = new[] { "id", "subject", "start", "end", "onlineMeeting" };
            }, ct),
            "GET /users/{mailbox}/calendarView?$select=onlineMeeting", ct);

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

    // ---------------- helpers ----------------

    private static DateTimeOffset ParseUtc(DateTimeTimeZone? z)
    {
        if (z?.DateTime is null) return DateTimeOffset.MinValue;
        return DateTimeOffset.Parse(z.DateTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    /// Refetch an event selecting only onlineMeeting; tiny retry for eventual consistency.
    private async Task<Event?> GetEventWithOnlineMeetingAsync(string mailbox, string eventId, CancellationToken ct)
    {
        Event? fetched = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            fetched = await RunGraph<Event?>(
                () => _graph.Users[mailbox].Events[eventId].GetAsync(cfg =>
                {
                    cfg.QueryParameters.Select = new[] { "onlineMeeting" };
                }, ct),
                "GET /users/{mailbox}/events/{id}?$select=onlineMeeting", ct);

            if (!string.IsNullOrEmpty(fetched?.OnlineMeeting?.JoinUrl))
                return fetched;

            await Task.Delay(400, ct);
        }
        return fetched;
    }

    private async Task EnsureTeamsMeetingDefaultsAsync(string mailbox, string eventId, Event? graphEvent, CancellationToken ct)
    {
        try
        {
            var ev = graphEvent;
            var onlineMeetingId = ev?.OnlineMeeting?.ConferenceId;

            if (string.IsNullOrWhiteSpace(onlineMeetingId))
            {
                ev = await _graph.Users[mailbox]
                    .Events[eventId]
                    .GetAsync(cfg =>
                    {
                        cfg.QueryParameters.Select = new[] { "onlineMeeting" };
                    }, ct);

                onlineMeetingId = ev?.OnlineMeeting?.ConferenceId;
            }

            if (string.IsNullOrWhiteSpace(onlineMeetingId))
            {
                _logger.LogWarning("Teams event {EventId} returned no online meeting id; unable to enforce recording/transcription defaults.", eventId);
                return;
            }

            var patch = new OnlineMeeting
            {
                AdditionalData = new Dictionary<string, object?>
                {
                    ["allowRecording"] = true,
                    ["recordAutomatically"] = true,
                    ["isTranscriptionEnabled"] = true
                }
            };

            await _graph.Users[mailbox]
                .OnlineMeetings[onlineMeetingId]
                .PatchAsync(patch, cancellationToken: ct);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "Failed to enforce Teams meeting defaults for event {EventId}.", eventId);
        }
    }

    // RunGraph overload for functions returning a value (v5 ApiException)
    private async Task<T?> RunGraph<T>(Func<Task<T?>> action, string op, CancellationToken ct) where T : class
    {
        try
        {
            return await action();
        }
        catch (ApiException ex)
        {
            // ApiException contains ResponseStatusCode, ResponseHeaders, Message
            var status = (int?)ex.ResponseStatusCode;
            var headers = ex.ResponseHeaders != null
                ? string.Join(", ", ex.ResponseHeaders.Select(kv => $"{kv.Key}={string.Join("|", kv.Value)}"))
                : "(no headers)";

            _logger.LogError(ex, "GRAPH {Op} failed: status={Status} message={Msg} headers={Headers}",
                op, status, ex.Message, headers);
            throw;
        }
    }

    // RunGraph overload for void-returning functions
    private async Task RunGraph(Func<Task> action, string op, CancellationToken ct)
    {
        try
        {
            await action();
        }
        catch (ApiException ex)
        {
            var status = (int?)ex.ResponseStatusCode;
            var headers = ex.ResponseHeaders != null
                ? string.Join(", ", ex.ResponseHeaders.Select(kv => $"{kv.Key}={string.Join("|", kv.Value)}"))
                : "(no headers)";

            _logger.LogError(ex, "GRAPH {Op} failed: status={Status} message={Msg} headers={Headers}",
                op, status, ex.Message, headers);
            throw;
        }
    }
}
