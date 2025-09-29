// Infrastructure/Calendars/ZoomCalendarService.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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


    public async Task<(string eventId, string? joinUrl)> CreateEventAsync(Meeting m, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        var host = string.IsNullOrWhiteSpace(m.ExternalCalendarMailbox) ? (_opts.HostUserId ?? "me") : m.ExternalCalendarMailbox!;


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
                waiting_room = true,
                join_before_host = false,
                approval_type = 2, // no registration required
                mute_upon_entry = true,
                auto_recording = "none"
            }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.zoom.us/v2/users/{Uri.EscapeDataString(host)}/meetings")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);


        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();


        var created = await resp.Content.ReadFromJsonAsync<ZoomCreateMeetingResponse>(cancellationToken: ct);
        if (created is null) throw new InvalidOperationException("Zoom didn't return meeting payload.");


        return (created.id.ToString(), created.join_url);
    }
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
        using var req = new HttpRequestMessage(HttpMethod.Patch, $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(m.ExternalEventId)}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);


        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();


        // Retrieve fresh join url
        using var getReq = new HttpRequestMessage(HttpMethod.Get, $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(m.ExternalEventId)}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getResp = await _http.SendAsync(getReq, ct);
        getResp.EnsureSuccessStatusCode();
        var meeting = await getResp.Content.ReadFromJsonAsync<ZoomCreateMeetingResponse>(cancellationToken: ct);
        return (true, meeting?.join_url);
    }
    public async Task CancelEventAsync(string eventId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return;
        var token = await GetAccessTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(eventId)}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }
    public async Task<IReadOnlyList<CalendarEventDto>> ListUpcomingAsync(int take = 20, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        var host = _opts.HostUserId ?? "me";


        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.zoom.us/v2/users/{Uri.EscapeDataString(host)}/meetings?type=upcoming&page_size={take}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();


        var data = await resp.Content.ReadFromJsonAsync<ZoomListMeetingsResponse>(cancellationToken: ct) ?? new ZoomListMeetingsResponse();


        return data.meetings.Select(m =>
        new CalendarEventDto(
        m.id.ToString(),
        m.topic ?? "(no subject)",
        m.start_time is null ? DateTimeOffset.MinValue : DateTimeOffset.Parse(m.start_time, null, System.Globalization.DateTimeStyles.RoundtripKind),
        m.start_time is null ? DateTimeOffset.MinValue : DateTimeOffset.Parse(m.start_time, null, System.Globalization.DateTimeStyles.RoundtripKind).AddMinutes(m.duration),
        m.join_url
        )).ToList();
    }
    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken != null && DateTimeOffset.UtcNow < _accessTokenExpiresAt - TimeSpan.FromMinutes(2))
            return _accessToken;


        var tokenUrl = $"https://zoom.us/oauth/token?grant_type=account_credentials&account_id={Uri.EscapeDataString(_opts.AccountId)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.ClientSecret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);


        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var tok = await resp.Content.ReadFromJsonAsync<ZoomTokenResponse>(cancellationToken: ct) ?? throw new InvalidOperationException("Zoom token response was null.");


        _accessToken = tok.access_token ?? throw new InvalidOperationException("Zoom access_token missing.");
        _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tok.expires_in);
        return _accessToken;
    }
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
        public List<ZoomCreateMeetingResponse> meetings { get; set; } = new();
    }
}