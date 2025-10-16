// File: Controllers/ZoomWebhookController.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using BoardMgmt.Application.Meetings.Commands;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace BoardMgmt.WebApi.Controllers
{
    [ApiController]
    [Route("webhooks/zoom")]
    public sealed class ZoomWebhookController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IAppDbContext _db;
        private readonly string _secretToken;
        private readonly ILogger<ZoomWebhookController> _logger;
        private readonly bool _disableSigValidation;
        private readonly IHostApplicationLifetime _appLifetime;

        public ZoomWebhookController(
            IMediator mediator,
            IAppDbContext db,
            IConfiguration config,
            ILogger<ZoomWebhookController> logger,
            IHostApplicationLifetime appLifetime)
        {
            _mediator = mediator;
            _db = db;
            _logger = logger;
            _appLifetime = appLifetime;

            _secretToken = config["Zoom:WebhookSecretToken"]
                ?? throw new InvalidOperationException("Zoom:WebhookSecretToken not configured.");

            // optional debug switch
            _disableSigValidation = config.GetValue("Zoom:DisableSignatureValidation", false);
        }

        [HttpPost("events")]
        [AllowAnonymous]
        public async Task<IActionResult> Receive(CancellationToken ct)
        {
            try
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, false, 1024, leaveOpen: true);
                var body = await reader.ReadToEndAsync(ct);

                _logger.LogInformation(
                    "Zoom webhook hit. Path={Path} Length={Length} HeadersTs={Ts} Sig={Sig}",
                    Request.Path,
                    body?.Length ?? 0,
                    Request.Headers["x-zm-request-timestamp"].ToString(),
                    Request.Headers["x-zm-signature"].ToString());

                if (string.IsNullOrWhiteSpace(body))
                {
                    _logger.LogWarning("Zoom webhook received empty body.");
                    return BadRequest("empty body");
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // (1) URL validation
                if (root.TryGetProperty("event", out var evEl) &&
                    AsString(evEl) == "endpoint.url_validation")
                {
                    if (!root.TryGetProperty("payload", out var p) ||
                        !p.TryGetProperty("plainToken", out var pt))
                    {
                        _logger.LogWarning("Zoom validation payload missing plainToken.");
                        return BadRequest();
                    }

                    var plainToken = AsString(pt) ?? string.Empty;
                    var encryptedToken = ComputeHmacSha256Hex(_secretToken, plainToken);

                    _logger.LogInformation("Zoom endpoint validation handled successfully.");
                    return Ok(new { plainToken, encryptedToken });
                }

                // (2) Signature verification
                if (!_disableSigValidation && !VerifyZoomSignature(Request.Headers, _secretToken, body))
                {
                    _logger.LogWarning("Zoom signature verification FAILED.");
                    return Unauthorized();
                }

                // (3) Safe JSON extraction
                if (!root.TryGetProperty("event", out var eventProp))
                {
                    _logger.LogWarning("Zoom payload missing 'event'.");
                    return Ok();
                }
                var eventType = AsString(eventProp) ?? "(unknown)";

                if (!root.TryGetProperty("payload", out var payload) ||
                    !payload.TryGetProperty("object", out var obj))
                {
                    _logger.LogWarning("Zoom payload missing 'payload.object'. Event={EventType}", eventType);
                    return Ok();
                }

                // id can be number or string; uuid should be string but we’ll be defensive
                string? meetingId = obj.TryGetProperty("id", out var idEl) ? AsString(idEl) : null;
                string? meetingUuid = obj.TryGetProperty("uuid", out var uuidEl) ? AsString(uuidEl) : null;

                _logger.LogInformation("Zoom event verified. Type={EventType} MeetingId={MeetingId} UUID={MeetingUuid} IdKind={IdKind}",
                    eventType, meetingId, meetingUuid, obj.TryGetProperty("id", out var idKindEl) ? idKindEl.ValueKind : JsonValueKind.Undefined);

                // (4) Find our meeting
                var ourMeeting = await _db.Set<Meeting>()
                    .Where(m => m.ExternalCalendar == "Zoom" &&
                                (m.ExternalEventId == meetingId || m.ExternalEventId == meetingUuid))
                    .Select(m => new { m.Id })
                    .FirstOrDefaultAsync(ct);

                if (ourMeeting is null)
                {
                    _logger.LogWarning("No matching meeting found. id={MeetingId} uuid={MeetingUuid}", meetingId, meetingUuid);
                    return Ok(); // ignore unknown
                }

                // (5) Handle events of interest
                if (eventType is "recording.completed" or "recording.transcript_completed")
                {
                    try
                    {
                        _logger.LogInformation("Starting transcript ingest. MeetingId={MeetingId}", ourMeeting.Id);
                        using var ingestCts = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopping);
                        await _mediator.Send(new IngestTranscriptCommand(ourMeeting.Id), ingestCts.Token);
                        _logger.LogInformation("Transcript ingest finished. MeetingId={MeetingId}", ourMeeting.Id);
                    }
                    catch (Exception exIngest)
                    {
                        _logger.LogError(exIngest, "Transcript ingest FAILED. MeetingId={MeetingId}", ourMeeting.Id);
                        return Ok(); // avoid Zoom retries; error is logged
                    }
                }
                else
                {
                    _logger.LogInformation("Event not handled in this controller. Type={EventType}", eventType);
                }

                return Ok();
            }
            catch (JsonException jx)
            {
                _logger.LogWarning(jx, "Invalid JSON body received from Zoom.");
                return BadRequest("invalid json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in Zoom webhook.");
                return Ok(); // keep Zoom from retrying; details are in Logs
            }
        }

        // ---- helpers ----

        // Accepts string/number/bool; returns a string or null.
        private static string? AsString(JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),                // preserve numeric text
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => el.GetRawText() // arrays/objects (not expected here) as JSON text
            };
        }

        private static string ComputeHmacSha256Hex(string secret, string message)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool VerifyZoomSignature(IHeaderDictionary headers, string secret, string rawBody)
        {
            var ts = headers["x-zm-request-timestamp"].FirstOrDefault();
            var sigHeader = headers["x-zm-signature"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(ts) || string.IsNullOrWhiteSpace(sigHeader)) return false;

            if (long.TryParse(ts, out var unix))
            {
                var eventTime = DateTimeOffset.FromUnixTimeSeconds(unix);
                if (DateTimeOffset.UtcNow - eventTime > TimeSpan.FromMinutes(5))
                    return false;
            }

            var baseString = $"v0:{ts}:{rawBody}";
            var expected = ComputeHmacSha256Hex(secret, baseString);
            var provided = sigHeader.Replace("v0=", "", StringComparison.OrdinalIgnoreCase);

            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var providedBytes = Encoding.UTF8.GetBytes(provided);
            if (expectedBytes.Length != providedBytes.Length) return false;
            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }
    }
}
