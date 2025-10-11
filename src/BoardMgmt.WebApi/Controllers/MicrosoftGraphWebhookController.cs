// File: Controllers/MicrosoftGraphWebhookController.cs
using System.Text;
using System.Text.Json;
using BoardMgmt.Application.Meetings.Commands;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoardMgmt.WebApi.Controllers
{
    [ApiController]
    [Route("webhooks/msgraph")]
    public sealed class MicrosoftGraphWebhookController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IAppDbContext _db;
        private readonly ILogger<MicrosoftGraphWebhookController> _log;
        private readonly string? _expectedClientState;

        public MicrosoftGraphWebhookController(
            IMediator mediator,
            IAppDbContext db,
            ILogger<MicrosoftGraphWebhookController> log,
            IConfiguration config)
        {
            _mediator = mediator;
            _db = db;
            _log = log;
            _expectedClientState = config["Graph:WebhookClientState"]; // optional
        }

        // ------------------------------------------------------------
        // (1) Graph validation handshake: MUST echo validationToken
        // ------------------------------------------------------------
        // Graph calls this first with: GET /webhooks/msgraph/events?validationToken=...
        [HttpGet("events")]
        [AllowAnonymous]
        public IActionResult Validate([FromQuery(Name = "validationToken")] string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest();

            _log.LogInformation("Graph webhook validation received.");
            return Content(token, "text/plain", Encoding.UTF8);
        }

        // ------------------------------------------------------------
        // (2) Notifications POST
        // ------------------------------------------------------------
        [HttpPost("events")]
        [AllowAnonymous]
        public async Task<IActionResult> Receive(CancellationToken ct)
        {
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
                body = await reader.ReadToEndAsync(ct);

            if (string.IsNullOrWhiteSpace(body))
            {
                _log.LogWarning("Graph webhook received empty body.");
                return Ok();
            }

            try
            {
                using var doc = JsonDocument.Parse(body); // ✅ correct form
                var root = doc.RootElement;

                if (root.TryGetProperty("value", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in list.EnumerateArray())
                    {
                        if (!VerifyClientState(item)) continue;
                        if (IsLifecycleNotification(item))
                        {
                            _log.LogInformation("Graph lifecycle notification: {LifecycleEvent}",
                                item.TryGetProperty("lifecycleEvent", out var le) ? le.GetString() : "(unknown)");
                            continue;
                        }

                        var resource = item.TryGetProperty("resource", out var r) ? r.GetString() : null;
                        var changeType = item.TryGetProperty("changeType", out var ch) ? ch.GetString() : null;

                        _log.LogInformation("Graph change notification: changeType={ChangeType}, resource={Resource}",
                            changeType, resource);

                        var onlineMeetingId = TryExtractBetween(resource, "/onlineMeetings('", "')");
                        var transcriptId = TryExtractBetween(resource, "/transcripts('", "')");

                        var meetingId = await ResolveOurMeetingIdAsync(onlineMeetingId, ct);
                        if (meetingId == Guid.Empty)
                        {
                            _log.LogWarning("No local meeting matched onlineMeetingId={OnlineMeetingId}", onlineMeetingId);
                            continue;
                        }

                        try
                        {
                            await _mediator.Send(new IngestTranscriptCommand(meetingId), ct);
                            _log.LogInformation("Microsoft365 transcript ingest triggered. MeetingId={MeetingId}", meetingId);
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Transcript ingest FAILED for MeetingId={MeetingId}", meetingId);
                        }
                    }
                }
            }
            catch (JsonException jx)
            {
                _log.LogWarning(jx, "Graph webhook invalid JSON.");
                return Ok();
            }

            return Ok();
        }


        // ---- helpers --------------------------------------------------------

        private bool VerifyClientState(JsonElement item)
        {
            if (string.IsNullOrWhiteSpace(_expectedClientState)) return true; // not configured -> skip
            var cs = item.TryGetProperty("clientState", out var c) ? c.GetString() : null;
            var ok = string.Equals(cs, _expectedClientState, StringComparison.Ordinal);
            if (!ok) _log.LogWarning("Graph clientState mismatch.");
            return ok;
        }

        private static bool IsLifecycleNotification(JsonElement item)
        {
            return item.TryGetProperty("lifecycleEvent", out var _);
        }

        private static string? TryExtractBetween(string source, string start, string end)
        {
            if (string.IsNullOrEmpty(source)) return null;
            var i = source.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            i += start.Length;
            var j = source.IndexOf(end, i, StringComparison.OrdinalIgnoreCase);
            if (j < 0) return null;
            return source.Substring(i, j - i);
        }

        // Strategy to map Graph notification -> your Meeting row.
        // Best: store OnlineMeetingId on your Meeting when you create the event.
        // Fallback shown below uses ExternalEventId + Mailbox if you persisted them.
        private async Task<Guid> ResolveOurMeetingIdAsync(string? onlineMeetingId, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(onlineMeetingId))
            {
                var found = await _db.Set<Meeting>()
                    .Where(m => m.ExternalCalendar == "Microsoft365" &&
                                m.ExternalEventId == onlineMeetingId) // <-- add this nullable column if not present
                    .Select(m => m.Id)
                    .FirstOrDefaultAsync(ct);

                if (found != Guid.Empty) return found;
            }

            // Fallback: If you didn’t store OnlineMeetingId, try to find the most
            // recent Teams meeting that has not been ingested yet (heuristic).
            // You can improve this by also checking ExternalEventId + mailbox.
            var recent = await _db.Set<Meeting>()
                .Where(m => m.ExternalCalendar == "Microsoft365" &&
                            (m.EndAt ?? m.ScheduledAt.AddHours(2)) > DateTimeOffset.UtcNow.AddHours(-12))
                .OrderByDescending(m => m.ScheduledAt)
                .Select(m => m.Id)
                .FirstOrDefaultAsync(ct);

            return recent;
        }
    }
}
