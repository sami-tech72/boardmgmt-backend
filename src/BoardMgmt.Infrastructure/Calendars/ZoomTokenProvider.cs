using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BoardMgmt.Application.Calendars;
using Microsoft.Extensions.Options;

namespace BoardMgmt.Infrastructure.Calendars;

public sealed class ZoomTokenProvider : IZoomTokenProvider
{
    private readonly HttpClient _http;
    private readonly ZoomOptions _opts;

    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public ZoomTokenProvider(IHttpClientFactory factory, IOptions<ZoomOptions> opts)
    {
        _http = factory.CreateClient("Zoom");
        _opts = opts.Value;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_accessToken != null && DateTimeOffset.UtcNow < _expiresAt - TimeSpan.FromMinutes(2))
            return _accessToken;

        var tokenUrl = $"https://zoom.us/oauth/token?grant_type=account_credentials&account_id={Uri.EscapeDataString(_opts.AccountId)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.ClientSecret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        _accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Zoom access_token missing.");
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

        return _accessToken;
    }
}
