using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoardMgmt.Application.Meetings.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;              // <— needed for IHeaderDictionary
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("webhooks/zoom")]
public sealed class ZoomWebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly DbContext _db;
    private readonly string _secretToken;

    public ZoomWebhookController(IMediator mediator, DbContext db, IConfiguration config)
    {
        _mediator = mediator;
        _db = db;
        _secretToken = config["Zoom:WebhookSecretToken"]
            ?? throw new InvalidOperationException("Zoom:WebhookSecretToken not configured.");
    }

    [HttpPost("events")]
    [AllowAnonymous]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        // Read raw body (needed for signature verification); pass the ct as the IDE suggests
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // 1) URL validation (endpoint.url_validation)
        if (root.TryGetProperty("event", out var eventEl) &&
            eventEl.GetString() == "endpoint.url_validation")
        {
            var plainToken = root.GetProperty("payload").GetProperty("plainToken").GetString()!;
            var encryptedToken = ComputeHmacSha256Hex(_secretToken, plainToken);
            return Ok(new { plainToken, encryptedToken });
        }

        // 2) Verify signature for all other events
        if (!VerifyZoomSignature(Request.Headers, _secretToken, body))   // <— fixed arg order
            return Unauthorized();

        // 3) Process event
        var eventType = root.GetProperty("event").GetString();
        var obj = root.GetProperty("payload").GetProperty("object");
        var meetingId = obj.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var meetingUuid = obj.TryGetProperty("uuid", out var uuidEl) ? uuidEl.GetString() : null;

        var ourMeeting = await _db.Set<BoardMgmt.Domain.Entities.Meeting>()
            .Where(m => m.ExternalCalendar == "Zoom" &&
                        (m.ExternalEventId == meetingId || m.ExternalEventId == meetingUuid))
            .Select(m => new { m.Id })
            .FirstOrDefaultAsync(ct);

        if (ourMeeting is null) return Ok(); // unknown to us; ignore

        if (eventType is "recording.transcript_completed" or "recording.completed")
        {
            await _mediator.Send(new IngestTranscriptCommand(ourMeeting.Id), ct);
        }

        return Ok();
    }

    // ---- helpers ----

    private static string ComputeHmacSha256Hex(string secret, string message)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool VerifyZoomSignature(IHeaderDictionary headers, string secret, string rawBody)
    {
        // Zoom sends:
        //   x-zm-request-timestamp: unix seconds
        //   x-zm-signature: v0=<hmac_hex>
        // Signature base: "v0:{timestamp}:{rawBody}"
        var ts = headers["x-zm-request-timestamp"].FirstOrDefault();
        var sigHeader = headers["x-zm-signature"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(ts) || string.IsNullOrWhiteSpace(sigHeader)) return false;

        // Optional replay protection (5 minutes)
        if (long.TryParse(ts, out var unix))
        {
            var eventTime = DateTimeOffset.FromUnixTimeSeconds(unix);
            if (DateTimeOffset.UtcNow - eventTime > TimeSpan.FromMinutes(5))
                return false;
        }

        var baseString = $"v0:{ts}:{rawBody}";
        var expected = ComputeHmacSha256Hex(secret, baseString);
        var provided = sigHeader.Replace("v0=", "", StringComparison.OrdinalIgnoreCase);

        // Constant-time compare
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        if (expectedBytes.Length != providedBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
