using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BoardMgmt.Application.Calendars;
using BoardMgmt.Domain.Entities;
using Microsoft.Extensions.Options;

namespace BoardMgmt.Infrastructure.Calendars;

public sealed class ZoomCalendarService : ICalendarService
{
    private readonly HttpClient _http;
    private readonly ZoomOptions _opts;

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    public ZoomCalendarService(IHttpClientFactory factory, IOptions<ZoomOptions> opts)
    {
        _http = factory.CreateClient("Zoom");
        _opts = opts.Value;
    }

    // ---------------------------
    // Create
    // ---------------------------
    public async Task<(string eventId, string? joinUrl)> CreateEventAsync(Meeting m, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        var host = string.IsNullOrWhiteSpace(m.ExternalCalendarMailbox)
            ? (_opts.HostUserId ?? "me")
            : m.ExternalCalendarMailbox!;

        var startUtc = m.ScheduledAt.ToUniversalTime();
        var end = m.EndAt ?? m.ScheduledAt.AddHours(1);
        var durationMinutes = (int)Math.Max(15, (end - m.ScheduledAt).TotalMinutes);

        var payload = new
        {
            topic = m.Title,
            type = 2, // scheduled meeting
            start_time = startUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            duration = durationMinutes,
            timezone = "UTC",
            agenda = m.Description,
            settings = new
            {
                host_video = true,
                participant_video = false,
                //waiting_room = true,
                //join_before_host = false,
                //approval_type = 2, // no registration required
                //mute_upon_entry = true,
                //auto_recording = "none"

                waiting_room = false,          // <— turn off waiting room
                join_before_host = true,       // <— allow join before the host arrives
                approval_type = 2,             // no registration required
                meeting_authentication = false,// <— no sign-in requirement
                mute_upon_entry = true,
                auto_recording = "none"
            }
        };

        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.zoom.us/v2/users/{Uri.EscapeDataString(host)}/meetings")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Zoom create meeting failed ({(int)resp.StatusCode} {resp.ReasonPhrase}) host={host}, body={body}");
        }

        var created = await resp.Content.ReadFromJsonAsync<ZoomCreateMeetingResponse>(cancellationToken: ct);
        if (created is null) throw new InvalidOperationException("Zoom didn't return meeting payload.");

        return (created.id.ToString(), created.join_url);
    }

    // ---------------------------
    // Update
    // ---------------------------
    public async Task<(bool ok, string? joinUrl)> UpdateEventAsync(Meeting m, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(m.ExternalEventId)) return (false, null);

        var token = await GetAccessTokenAsync(ct);
        var startUtc = m.ScheduledAt.ToUniversalTime();
        var end = m.EndAt ?? m.ScheduledAt.AddHours(1);
        var durationMinutes = (int)Math.Max(15, (end - m.ScheduledAt).TotalMinutes);

        var payload = new
        {
            topic = m.Title,
            start_time = startUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            duration = durationMinutes,
            timezone = "UTC",
            agenda = m.Description
        };

        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(m.ExternalEventId)}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Zoom update meeting failed ({(int)resp.StatusCode} {resp.ReasonPhrase}) id={m.ExternalEventId}, body={body}");
        }

        // Get fresh join URL
        using var getReq = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(m.ExternalEventId)}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var getResp = await _http.SendAsync(getReq, ct);
        if (!getResp.IsSuccessStatusCode)
        {
            var body = await getResp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Zoom get meeting failed ({(int)getResp.StatusCode} {getResp.ReasonPhrase}) id={m.ExternalEventId}, body={body}");
        }

        var meeting = await getResp.Content.ReadFromJsonAsync<ZoomCreateMeetingResponse>(cancellationToken: ct);
        return (true, meeting?.join_url);
    }

    // ---------------------------
    // Delete
    // ---------------------------
    public async Task CancelEventAsync(string eventId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return;

        var token = await GetAccessTokenAsync(ct);
        using var req = new HttpRequestMessage(
            HttpMethod.Delete,
            $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(eventId)}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Zoom delete meeting failed ({(int)resp.StatusCode} {resp.ReasonPhrase}) id={eventId}, body={body}");
        }
    }

    // ---------------------------
    // Upcoming (simple)
    // ---------------------------
    public async Task<IReadOnlyList<CalendarEventDto>> ListUpcomingAsync(int take = 20, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        var host = _opts.HostUserId ?? "me";

        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.zoom.us/v2/users/{Uri.EscapeDataString(host)}/meetings?type=upcoming&page_size={take}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Zoom list upcoming failed ({(int)resp.StatusCode} {resp.ReasonPhrase}) host={host}, body={body}");
        }

        var data = await resp.Content.ReadFromJsonAsync<ZoomListMeetingsResponse>(cancellationToken: ct)
                   ?? new ZoomListMeetingsResponse();

        var meetings = data.meetings ?? new();
        return meetings.Select(m =>
        {
            var start = string.IsNullOrWhiteSpace(m.start_time)
                ? (DateTimeOffset?)null
                : DateTimeOffset.Parse(m.start_time, null, System.Globalization.DateTimeStyles.RoundtripKind);
            var end = start.HasValue ? start.Value.AddMinutes(Math.Max(1, m.duration)) : DateTimeOffset.MinValue;

            return new CalendarEventDto(
                m.id.ToString(),
                string.IsNullOrWhiteSpace(m.topic) ? "(no subject)" : m.topic!,
                start ?? DateTimeOffset.MinValue,
                end,
                string.IsNullOrWhiteSpace(m.join_url) ? null : m.join_url,
                "Zoom");
        }).ToList();
    }

    // ---------------------------
    // Range (merge upcoming + past, then filter)
    // ---------------------------
    public async Task<IReadOnlyList<CalendarEventDto>> ListRangeAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        var host = _opts.HostUserId ?? "me";   // Prefer a real email in config for reliability

        async Task<List<ZoomCreateMeetingResponse>> Fetch(string type, int pageSize = 100)
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.zoom.us/v2/users/{Uri.EscapeDataString(host)}/meetings?type={type}&page_size={pageSize}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Zoom list meetings failed ({(int)resp.StatusCode} {resp.ReasonPhrase}) type={type}, host={host}, body={body}");
            }

            var data = await resp.Content.ReadFromJsonAsync<ZoomListMeetingsResponse>(cancellationToken: ct)
                       ?? new ZoomListMeetingsResponse();
            return data.meetings ?? new();
        }

        // Valid types: scheduled | live | upcoming | upcoming_meetings | past
        var upcoming = await Fetch("upcoming");
        var past = await Fetch("past");

        var all = upcoming.Concat(past).ToList();

        static DateTimeOffset? ParseStart(string? s)
            => string.IsNullOrWhiteSpace(s) ? null
               : DateTimeOffset.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind);

        var filtered = all
            .Select(m =>
            {
                var start = ParseStart(m.start_time);
                var end = start.HasValue ? start.Value.AddMinutes(Math.Max(1, m.duration)) : (DateTimeOffset?)null;
                return new { m, start, end };
            })
            .Where(x => x.start.HasValue && x.end.HasValue
                        && x.end.Value > startUtc
                        && x.start.Value < endUtc)
            .OrderBy(x => x.start)
            .Select(x => new CalendarEventDto(
                x.m.id.ToString(),
                string.IsNullOrWhiteSpace(x.m.topic) ? "(no subject)" : x.m.topic!,
                x.start!.Value,
                x.end!.Value,
                string.IsNullOrWhiteSpace(x.m.join_url) ? null : x.m.join_url,
                "Zoom"))
            .ToList();

        return filtered;
    }

    // ---------------------------
    // Token (account_credentials)
    // ---------------------------
    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken != null && DateTimeOffset.UtcNow < _accessTokenExpiresAt - TimeSpan.FromMinutes(2))
            return _accessToken;

        var tokenUrl = $"https://zoom.us/oauth/token?grant_type=account_credentials&account_id={Uri.EscapeDataString(_opts.AccountId)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.ClientSecret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Zoom token failed ({(int)resp.StatusCode} {resp.ReasonPhrase}) body={body}");
        }

        var tok = await resp.Content.ReadFromJsonAsync<ZoomTokenResponse>(cancellationToken: ct)
                  ?? throw new InvalidOperationException("Zoom token response was null.");

        _accessToken = tok.access_token ?? throw new InvalidOperationException("Zoom access_token missing.");
        _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tok.expires_in);
        return _accessToken;
    }

    // ---------------------------
    // DTOs
    // ---------------------------
    private sealed class ZoomTokenResponse
    {
        public string? access_token { get; set; }
        public int expires_in { get; set; }
        public string? token_type { get; set; }
        public string? scope { get; set; }
    }

    private sealed class ZoomCreateMeetingResponse
    {
        public long id { get; set; }
        public string? uuid { get; set; }
        public string? join_url { get; set; }
        public string? start_url { get; set; }
        public string? topic { get; set; }
        public string? start_time { get; set; }
        public int duration { get; set; }
    }

    private sealed class ZoomListMeetingsResponse
    {
        public List<ZoomCreateMeetingResponse>? meetings { get; set; }
    }
}
