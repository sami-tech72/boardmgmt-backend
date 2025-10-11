using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
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
    private static readonly Regex TeamsJoinUrlRegex = new(
        "https://teams\\.microsoft\\.com/l/meetup-join/[^\\s\"\\<]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ILogger<Microsoft365CalendarService> _logger;
    private bool? _supportsOnlineMeetingExpand;

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

        // Ensure the meeting keeps track of the mailbox that actually hosts the
        // Teams event.  The transcript ingest workflow relies on
        // Meeting.ExternalCalendarMailbox being populated; if the caller did not
        // provide a HostIdentity when creating the meeting we still fall back to
        // the configured default mailbox.  Persisting it here means later
        // handlers (such as IngestTranscriptHandler) can safely resolve the
        // online meeting without requiring additional context.
        m.ExternalCalendarMailbox = mailbox;

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
        var created = await RunGraph<Event>(
            () => _graph.Users[mailbox].Events.PostAsync(ev, cancellationToken: ct),
            "POST /users/{mailbox}/events", ct);

        var id = created?.Id ?? throw new InvalidOperationException("Graph didn't return Event Id.");

        // Refetch with $select=onlineMeeting,onlineMeetingUrl (short retry for consistency)
        var fetched = await GetEventWithOnlineMeetingAsync(mailbox!, id, ct);
        var meeting = await EnsureTeamsMeetingDefaultsAsync(mailbox!, id, fetched, ct);
        var joinUrl = await ResolveJoinUrlAsync(mailbox!, id, fetched, meeting, ct);

        if (string.IsNullOrWhiteSpace(joinUrl))
        {
            _logger.LogWarning("Teams meeting link was not generated for newly created event {EventId}.", id);
        }

        return (id, joinUrl);
    }

    public async Task<(bool ok, string? joinUrl)> UpdateEventAsync(Meeting m, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(m.ExternalEventId)) return (false, null);

        var end = m.EndAt ?? m.ScheduledAt.AddHours(1);
        var mailbox = string.IsNullOrWhiteSpace(m.ExternalCalendarMailbox)
            ? _opts.MailboxAddress
            : m.ExternalCalendarMailbox;

        // Mirror the create logic so updates continue to persist whichever
        // mailbox we ultimately act against.  This protects against meetings
        // created before the create logic populated the mailbox and ensures
        // subsequent transcript ingestion has the data it needs.
        m.ExternalCalendarMailbox = mailbox;

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
        var meeting = await EnsureTeamsMeetingDefaultsAsync(mailbox!, m.ExternalEventId!, refreshed, ct);

        var joinUrl = await ResolveJoinUrlAsync(mailbox!, m.ExternalEventId!, refreshed, meeting, ct);
        if (string.IsNullOrWhiteSpace(joinUrl))
        {
            _logger.LogWarning("Teams meeting link was not generated for updated event {EventId}.", m.ExternalEventId);
        }

        return (true, joinUrl);
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

        var resp = await RunGraph<EventCollectionResponse>(
            () => _graph.Users[_opts.MailboxAddress].CalendarView.GetAsync(cfg =>
            {
                cfg.QueryParameters.StartDateTime = now.ToString("o");
                cfg.QueryParameters.EndDateTime = now.AddDays(30).ToString("o");
                cfg.QueryParameters.Top = take;
                cfg.QueryParameters.Orderby = new[] { "start/dateTime" };
                cfg.QueryParameters.Select = new[] { "id", "subject", "start", "end", "onlineMeeting", "onlineMeetingUrl" };
            }, ct),
            "GET /users/{mailbox}/calendarView?$select=onlineMeeting,onlineMeetingUrl", ct);

        var list = resp?.Value ?? new List<Event>();
        return list.Select(e => new CalendarEventDto(
            e.Id!,
            e.Subject ?? "(no subject)",
            ParseUtc(e.Start),
            ParseUtc(e.End),
            ExtractJoinUrl(e),
            "Microsoft365"
        )).ToList();
    }

    public async Task<IReadOnlyList<CalendarEventDto>> ListRangeAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default)
    {
        var resp = await RunGraph<EventCollectionResponse>(
            () => _graph.Users[_opts.MailboxAddress].CalendarView.GetAsync(cfg =>
            {
                cfg.QueryParameters.StartDateTime = startUtc.ToString("o");
                cfg.QueryParameters.EndDateTime = endUtc.ToString("o");
                cfg.QueryParameters.Orderby = new[] { "start/dateTime" };
                cfg.QueryParameters.Top = 100;
                cfg.QueryParameters.Select = new[] { "id", "subject", "start", "end", "onlineMeeting", "onlineMeetingUrl" };
            }, ct),
            "GET /users/{mailbox}/calendarView?$select=onlineMeeting,onlineMeetingUrl", ct);

        var list = resp?.Value ?? new List<Event>();
        return list.Select(e => new CalendarEventDto(
            e.Id!,
            e.Subject ?? "(no subject)",
            ParseUtc(e.Start),
            ParseUtc(e.End),
            ExtractJoinUrl(e),
            "Microsoft365"
        )).ToList();
    }

    // ---------------- helpers ----------------

    private static DateTimeOffset ParseUtc(DateTimeTimeZone? z)
    {
        if (z?.DateTime is null) return DateTimeOffset.MinValue;
        return DateTimeOffset.Parse(z.DateTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    /// Refetch an event selecting Teams meeting metadata; tiny retry for eventual consistency.
    private async Task<Event?> GetEventWithOnlineMeetingAsync(string mailbox, string eventId, CancellationToken ct)
    {
        Event? fetched = null;
        var attempt = 0;
        var delayMs = 300;
        while (attempt < 10)
        {
            var tryExpand = _supportsOnlineMeetingExpand != false;

            try
            {
                fetched = await RunGraph<Event>(
                    () => _graph.Users[mailbox].Events[eventId].GetAsync(cfg =>
                    {
                        cfg.QueryParameters.Select = new[] { "onlineMeeting", "onlineMeetingUrl" };
                        if (tryExpand)
                        {
                            cfg.QueryParameters.Expand = new[] { "onlineMeeting" };
                        }
                    }, ct),
                    "GET /users/{mailbox}/events/{id}?$select=onlineMeeting,onlineMeetingUrl", ct,
                    ex => tryExpand && IsOnlineMeetingExpandUnsupported(ex));

                if (tryExpand)
                {
                    _supportsOnlineMeetingExpand = true;
                }
            }
            catch (ApiException ex) when (tryExpand && IsOnlineMeetingExpandUnsupported(ex))
            {
                _supportsOnlineMeetingExpand = false;
                _logger.LogInformation("Microsoft Graph rejected $expand=onlineMeeting; retrying without expand.");
                continue;
            }

            if (!string.IsNullOrEmpty(ExtractJoinUrl(fetched)))
                return fetched;

            attempt++;
            await Task.Delay(delayMs, ct);
            delayMs = Math.Min(delayMs * 2, 2000);
        }
        return fetched;
    }

    private static string? ExtractJoinUrl(Event? ev, OnlineMeeting? meeting = null)
    {
        if (!string.IsNullOrWhiteSpace(ev?.OnlineMeeting?.JoinUrl))
            return ev!.OnlineMeeting!.JoinUrl;

        if (!string.IsNullOrWhiteSpace(ev?.OnlineMeetingUrl))
            return ev!.OnlineMeetingUrl;

        if (TryGetString(ev?.AdditionalData, "onlineMeetingUrl", out var fromRoot))
            return fromRoot;

        if (TryGetNestedString(ev?.AdditionalData, "onlineMeeting", "joinUrl", out var fromNested))
            return fromNested;

        return ExtractJoinUrl(meeting);
    }

    private static string? ExtractJoinUrl(OnlineMeeting? meeting)
    {
        if (!string.IsNullOrWhiteSpace(meeting?.JoinWebUrl))
            return meeting!.JoinWebUrl;

        if (TryGetString(meeting?.AdditionalData, "joinWebUrl", out var joinWebUrl))
            return joinWebUrl;

        if (TryGetString(meeting?.AdditionalData, "joinUrl", out var joinUrl))
            return joinUrl;

        if (TryGetNestedString(meeting?.AdditionalData, "joinInformation", "content", out var joinInfoContent))
            return joinInfoContent;

        return null;
    }

    private static bool TryGetNestedString(IDictionary<string, object?>? data, string key, string nestedKey, out string? value)
    {
        value = null;
        if (data is null || !data.TryGetValue(key, out var nested) || nested is null)
            return false;

        switch (nested)
        {
            case IDictionary<string, object?> dict:
                return TryGetString(dict, nestedKey, out value);
            case JsonElement element when element.ValueKind == JsonValueKind.Object:
                if (element.TryGetProperty(nestedKey, out var nestedEl) && nestedEl.ValueKind == JsonValueKind.String)
                {
                    var str = nestedEl.GetString();
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        value = str;
                        return true;
                    }
                }
                break;
        }

        return false;
    }

    private static bool TryGetString(IDictionary<string, object?>? data, string key, out string? value)
    {
        value = null;
        if (data is null || !data.TryGetValue(key, out var raw) || raw is null)
            return false;

        switch (raw)
        {
            case string str when !string.IsNullOrWhiteSpace(str):
                value = str;
                return true;
            case JsonElement el when el.ValueKind == JsonValueKind.String:
                var strVal = el.GetString();
                if (!string.IsNullOrWhiteSpace(strVal))
                {
                    value = strVal;
                    return true;
                }
                break;
        }

        return false;
    }

    private async Task<OnlineMeeting?> EnsureTeamsMeetingDefaultsAsync(string mailbox, string eventId, Event? graphEvent, CancellationToken ct)
    {
        var attempt = 0;
        var delayMs = 300;
        OnlineMeeting? resolvedMeeting = null;
        while (attempt < 6)
        {
            try
            {
                var (onlineMeetingId, meeting) = await ResolveOnlineMeetingAsync(mailbox, eventId, graphEvent, ct);
                if (resolvedMeeting is null && meeting is not null)
                {
                    resolvedMeeting = meeting;
                }

                if (string.IsNullOrWhiteSpace(onlineMeetingId))
                {
                    attempt++;
                    if (attempt >= 6)
                    {
                        _logger.LogWarning("Teams event {EventId} returned no online meeting id; unable to enforce recording/transcription defaults.", eventId);
                        return resolvedMeeting;
                    }

                    await Task.Delay(delayMs, ct);
                    delayMs = Math.Min(delayMs * 2, 2000);
                    continue;
                }

                resolvedMeeting ??= await GetEventOnlineMeetingAsync(mailbox, eventId, ct);

                var patch = BuildOnlineMeetingDefaultsPatch(includeTranscription: true);

                try
                {
                    await _graph.Users[mailbox]
                        .OnlineMeetings[onlineMeetingId]
                        .PatchAsync(patch, cancellationToken: ct);
                }
                catch (ApiException ex) when (IsMissingTranscriptPermission(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Unable to enable Teams transcription for event {EventId} because the app registration lacks the required Graph permissions. Falling back to recording-only defaults.",
                        eventId);

                    var fallbackPatch = BuildOnlineMeetingDefaultsPatch(includeTranscription: false);

                    await _graph.Users[mailbox]
                        .OnlineMeetings[onlineMeetingId]
                        .PatchAsync(fallbackPatch, cancellationToken: ct);
                }
                return resolvedMeeting;
            }
            catch (ApiException ex) when (attempt < 5)
            {
                _logger.LogInformation(ex, "Retrying Teams meeting defaults enforcement for event {EventId} after transient Graph error.", eventId);
            }
            catch (ApiException ex)
            {
                _logger.LogWarning(ex, "Failed to enforce Teams meeting defaults for event {EventId}.", eventId);
                return resolvedMeeting;
            }

            attempt++;
            await Task.Delay(delayMs, ct);
            delayMs = Math.Min(delayMs * 2, 2000);
        }
        return resolvedMeeting;
    }

    private static OnlineMeeting BuildOnlineMeetingDefaultsPatch(bool includeTranscription)
    {
        var additionalData = new Dictionary<string, object?>
        {
            ["allowRecording"] = true,
            ["recordAutomatically"] = true
        };

        if (includeTranscription)
        {
            additionalData["isTranscriptionEnabled"] = true;
        }

        return new OnlineMeeting
        {
            AdditionalData = additionalData
        };
    }

    private static bool IsMissingTranscriptPermission(ApiException ex)
    {
        if (ex.ResponseStatusCode != HttpStatusCode.Forbidden && ex.ResponseStatusCode != HttpStatusCode.InternalServerError)
        {
            return false;
        }

        var message = ex.Message ?? string.Empty;
        var body = ex.ResponseBody ?? string.Empty;

        return message.Contains("OnlineMeetingTranscript.Read.All", StringComparison.OrdinalIgnoreCase)
            || message.Contains("OnlineMeetingTranscript.Read.Chat", StringComparison.OrdinalIgnoreCase)
            || body.Contains("OnlineMeetingTranscript.Read.All", StringComparison.OrdinalIgnoreCase)
            || body.Contains("OnlineMeetingTranscript.Read.Chat", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(string? meetingId, OnlineMeeting? meeting)> ResolveOnlineMeetingAsync(string mailbox, string eventId, Event? ev, CancellationToken ct)
    {
        var fetchedEvent = ev;

        if (fetchedEvent is null)
        {
            fetchedEvent = await _graph.Users[mailbox]
                .Events[eventId]
                .GetAsync(cfg =>
                {
                    cfg.QueryParameters.Select = new[] { "onlineMeeting" };
                }, ct);
        }

        if (TryGetString(fetchedEvent?.AdditionalData, "onlineMeetingId", out var rootId) && !string.IsNullOrWhiteSpace(rootId))
            return (rootId, null);

        if (TryGetNestedString(fetchedEvent?.AdditionalData, "onlineMeeting", "id", out var nestedId) && !string.IsNullOrWhiteSpace(nestedId))
            return (nestedId, null);

        if (TryGetString(fetchedEvent?.OnlineMeeting?.AdditionalData, "id", out var directId) && !string.IsNullOrWhiteSpace(directId))
            return (directId, null);

        if (TryGetNestedString(fetchedEvent?.AdditionalData, "onlineMeeting", "onlineMeetingId", out var nestedMeetingId) && !string.IsNullOrWhiteSpace(nestedMeetingId))
            return (nestedMeetingId, null);

        try
        {
            var meeting = await GetEventOnlineMeetingAsync(mailbox, eventId, ct);

            if (!string.IsNullOrWhiteSpace(meeting?.Id))
                return (meeting!.Id, meeting);

            return (null, meeting);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound)
        {
            // Event exists but Graph hasn't linked an online meeting yet.
        }

        return (null, null);
    }

    private async Task<string?> ResolveJoinUrlAsync(string mailbox, string eventId, Event? ev, OnlineMeeting? meeting, CancellationToken ct)
    {
        var joinUrl = ExtractJoinUrl(ev, meeting);
        if (!string.IsNullOrWhiteSpace(joinUrl))
            return joinUrl;

        if (meeting is null)
        {
            try
            {
                meeting = await GetEventOnlineMeetingAsync(mailbox, eventId, ct);
            }
            catch (ApiException ex) when (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound)
            {
                return await TryExtractJoinUrlFromBodyAsync(mailbox, eventId, ct);
            }
        }

        joinUrl = ExtractJoinUrl(meeting);
        if (!string.IsNullOrWhiteSpace(joinUrl))
            return joinUrl;

        return await TryExtractJoinUrlFromBodyAsync(mailbox, eventId, ct);
    }

    private Task<OnlineMeeting?> GetEventOnlineMeetingAsync(string mailbox, string eventId, CancellationToken ct)
    {
        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.GET,
            UrlTemplate = "{+baseurl}/users/{user%2Did}/events/{event%2Did}/onlineMeeting",
        };

        requestInfo.PathParameters["user%2Did"] = mailbox;
        requestInfo.PathParameters["event%2Did"] = eventId;

        return RunGraph(
            () => _graph.RequestAdapter.SendAsync(
                requestInfo,
                OnlineMeeting.CreateFromDiscriminatorValue,
                cancellationToken: ct),
            "GET /users/{mailbox}/events/{id}/onlineMeeting",
            ct);
    }

    private async Task<string?> TryExtractJoinUrlFromBodyAsync(string mailbox, string eventId, CancellationToken ct)
    {
        var ev = await RunGraph<Event>(
            () => _graph.Users[mailbox].Events[eventId].GetAsync(cfg =>
            {
                cfg.QueryParameters.Select = new[] { "body" };
            }, ct),
            "GET /users/{mailbox}/events/{id}?$select=body",
            ct);

        var body = ev?.Body?.Content;
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var match = TeamsJoinUrlRegex.Match(body);
        if (!match.Success)
            return null;

        var decoded = WebUtility.HtmlDecode(match.Value);
        return string.IsNullOrWhiteSpace(decoded) ? null : decoded;
    }

    // RunGraph overload for functions returning a value (v5 ApiException)
    private async Task<T?> RunGraph<T>(
        Func<Task<T?>> action,
        string op,
        CancellationToken ct,
        Func<ApiException, bool>? suppressLogPredicate = null) where T : class
    {
        try
        {
            var result = await action();
            _logger.LogInformation("GRAPH {Op} succeeded.", op);
            return result;
        }
        catch (ApiException ex)
        {
            // ApiException contains ResponseStatusCode, ResponseHeaders, Message
            var status = (int?)ex.ResponseStatusCode;
            var headers = ex.ResponseHeaders != null
                ? string.Join(", ", ex.ResponseHeaders.Select(kv => $"{kv.Key}={string.Join("|", kv.Value)}"))
                : "(no headers)";

            if (suppressLogPredicate?.Invoke(ex) != true)
            {
                _logger.LogError(ex, "GRAPH {Op} failed: status={Status} message={Msg} headers={Headers}",
                    op, status, ex.Message, headers);
            }
            throw;
        }
    }

    // RunGraph overload for void-returning functions
    private async Task RunGraph(
        Func<Task> action,
        string op,
        CancellationToken ct,
        Func<ApiException, bool>? suppressLogPredicate = null)
    {
        try
        {
            await action();
            _logger.LogInformation("GRAPH {Op} succeeded.", op);
        }
        catch (ApiException ex)
        {
            var status = (int?)ex.ResponseStatusCode;
            var headers = ex.ResponseHeaders != null
                ? string.Join(", ", ex.ResponseHeaders.Select(kv => $"{kv.Key}={string.Join("|", kv.Value)}"))
                : "(no headers)";

            if (suppressLogPredicate?.Invoke(ex) != true)
            {
                _logger.LogError(ex, "GRAPH {Op} failed: status={Status} message={Msg} headers={Headers}",
                    op, status, ex.Message, headers);
            }
            throw;
        }
    }

    private static bool IsOnlineMeetingExpandUnsupported(ApiException ex)
    {
        if (ex.ResponseStatusCode != (int)HttpStatusCode.BadRequest)
            return false;

        return ex.Message?.Contains("Only navigation properties can be expanded", StringComparison.OrdinalIgnoreCase) == true
            && ex.Message.Contains("'onlineMeeting'", StringComparison.OrdinalIgnoreCase);
    }
}
