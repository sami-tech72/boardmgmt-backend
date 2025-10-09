using System;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Common.Email;
using BoardMgmt.Application.Common.Options;
using BoardMgmt.Application.Common.Parsing; // SimpleVtt
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace BoardMgmt.Application.Meetings.Commands
{
    public sealed class IngestTranscriptHandler(
        IAppDbContext db,
        GraphServiceClient graph,
        IHttpClientFactory httpFactory,
        IZoomTokenProvider zoomTokenProvider,
        IEmailSender email,
        IOptions<AppOptions> app)
        : IRequestHandler<IngestTranscriptCommand, int>
    {
        private readonly IAppDbContext _db = db;
        private readonly GraphServiceClient _graph = graph;
        private readonly IHttpClientFactory _httpFactory = httpFactory;
        private readonly IZoomTokenProvider _zoomTokenProvider = zoomTokenProvider;
        private readonly IEmailSender _email = email;
        private readonly AppOptions _app = app.Value ?? new AppOptions();

        public async Task<int> Handle(IngestTranscriptCommand request, CancellationToken ct)
        {
            var meeting = await _db.Set<Meeting>()
                .Include(m => m.Attendees)
                .FirstOrDefaultAsync(m => m.Id == request.MeetingId, ct)
                ?? throw new InvalidOperationException("Meeting not found.");

            if (string.IsNullOrWhiteSpace(meeting.ExternalCalendar))
                throw new InvalidOperationException("Meeting.ExternalCalendar not set.");

            if (string.IsNullOrWhiteSpace(meeting.ExternalEventId))
                throw new InvalidOperationException("Meeting.ExternalEventId not set.");

            var count = meeting.ExternalCalendar switch
            {
                "Microsoft365" => await IngestTeams(meeting, ct),
                "Zoom" => await IngestZoom(meeting, ct),
                _ => throw new InvalidOperationException($"Unsupported provider: {meeting.ExternalCalendar}")
            };

            // After saving: load and email a summary + attach VTT
            await EmailTranscriptAsync(meeting, ct);

            return count;
        }

        // --------------------------------------------------------------------
        // Microsoft Teams (Graph)
        // --------------------------------------------------------------------
        private async Task<int> IngestTeams(Meeting meeting, CancellationToken ct)
        {
            var mailbox = meeting.ExternalCalendarMailbox;
            if (string.IsNullOrWhiteSpace(mailbox))
                throw new InvalidOperationException(
                    "Meeting.ExternalCalendarMailbox is required for Teams transcript ingestion (set HostIdentity when creating the meeting).");

            var onlineMeetingId = await ResolveTeamsOnlineMeetingIdAsync(mailbox!, meeting, ct);

            // List transcripts (ONLINE MEETING scope)
            var list = await _graph.Users[mailbox]
                .OnlineMeetings[onlineMeetingId]
                .Transcripts
                .GetAsync(cancellationToken: ct);

            var tr = list?.Value?.FirstOrDefault()
                ?? throw new InvalidOperationException("No transcript found for this Teams meeting. Ensure transcription was enabled.");

            // Download VTT
            using var stream = await _graph.Users[mailbox]
                .OnlineMeetings[onlineMeetingId]
                .Transcripts[tr.Id!]
                .Content
                .GetAsync(cancellationToken: ct)
                ?? throw new InvalidOperationException("Teams transcript download returned no content stream.");

            using var reader = new System.IO.StreamReader(stream);
            var vtt = await reader.ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(vtt))
                throw new InvalidOperationException("Teams returned an empty transcript content.");

            return await SaveVtt(meeting, "Microsoft365", tr.Id!, vtt, ct);
        }

        private async Task<string> ResolveTeamsOnlineMeetingIdAsync(string mailbox, Meeting meeting, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(meeting.ExternalEventId))
                throw new InvalidOperationException("Meeting.ExternalEventId not set.");

            try
            {
                var graphEvent = await _graph.Users[mailbox]
                    .Events[meeting.ExternalEventId]
                    .GetAsync(cfg =>
                    {
                        cfg.QueryParameters.Expand = new[] { "onlineMeeting" };
                        cfg.QueryParameters.Select = new[] { "onlineMeeting" };
                    }, ct);

                var id = graphEvent?.OnlineMeeting?.ConferenceId;
                if (!string.IsNullOrWhiteSpace(id))
                    return id!;
            }
            catch (ServiceException ex) when (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("Teams meeting not found when resolving online meeting id. Verify the mailbox and meeting id.", ex);
            }

            throw new InvalidOperationException("Teams meeting is missing an online meeting id. Ensure the event is a Teams meeting with transcription enabled.");
        }

        // --------------------------------------------------------------------
        // Zoom (Recordings API)
        // --------------------------------------------------------------------
        private async Task<int> IngestZoom(Meeting meeting, CancellationToken ct)
        {
            var http = _httpFactory.CreateClient("Zoom");
            var token = await _zoomTokenProvider.GetAccessTokenAsync(ct);

            // 1) Try recordings by meeting id
            var doc = await TryGetJson(
                http, token,
                $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(meeting.ExternalEventId!)}/recordings",
                ct);

            if (doc is not null)
                return await ExtractAndSaveZoomTranscriptOrThrow(meeting, http, token, doc, ct);

            // 2) Get meeting details (for base UUID)
            var meetingDetail = await GetJsonOrThrow(
                http, token,
                $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(meeting.ExternalEventId!)}",
                ct,
                on404: "Zoom didn’t recognize this meeting id. Verify the host and app scopes (meeting:read:admin).");

            var baseUuid = meetingDetail.RootElement.TryGetProperty("uuid", out var uuidEl)
                ? uuidEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(baseUuid))
                throw new InvalidOperationException("Zoom didn’t return a meeting UUID. Verify the meeting id and app scopes.");

            // 3) List past instances for UUID
            var instances = await GetJsonOrThrow(
                http, token,
                $"https://api.zoom.us/v2/past_meetings/{Uri.EscapeDataString(Uri.EscapeDataString(baseUuid))}/instances",
                ct,
                on404: "Zoom returned no past instances for this UUID. If you didn’t record to cloud, no transcript will exist.");

            if (!instances.RootElement.TryGetProperty("meetings", out var arr) ||
                arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
                throw new InvalidOperationException("No past instances returned for this meeting UUID. Was the meeting held and recorded to the cloud?");

            // 4) Try each instance's recordings
            foreach (var inst in arr.EnumerateArray())
            {
                var instUuid = inst.TryGetProperty("uuid", out var iu) ? iu.GetString() : null;
                if (string.IsNullOrWhiteSpace(instUuid)) continue;

                var safeUuid = Uri.EscapeDataString(Uri.EscapeDataString(instUuid));
                var recDoc = await TryGetJson(http, token,
                    $"https://api.zoom.us/v2/past_meetings/{safeUuid}/recordings",
                    ct);
                if (recDoc is null) continue;

                var saved = await TryExtractAndSaveZoomTranscript(meeting, http, token, recDoc, ct);
                if (saved.HasValue) return saved.Value;
            }

            throw new InvalidOperationException(
                "No cloud transcript file found for this meeting. " +
                "Enable 'Cloud recording' and 'Create audio transcript', record to cloud, wait for processing, then try again.");
        }

        // -------------------- Zoom helpers --------------------
        private static async Task<JsonDocument?> TryGetJson(HttpClient http, string token, string url, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return JsonDocument.Parse(json);
            }
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            var body = await resp.Content.ReadAsStringAsync(ct) ?? string.Empty;
            throw new HttpRequestException($"Zoom query failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {SummarizeZoomError(body)}");
        }

        private static async Task<JsonDocument> GetJsonOrThrow(HttpClient http, string token, string url, CancellationToken ct, string? on404 = null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(json))
                    throw new InvalidOperationException("Zoom returned an empty response body.");

                return JsonDocument.Parse(json);
            }

            var body = await resp.Content.ReadAsStringAsync(ct) ?? string.Empty;
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound && !string.IsNullOrWhiteSpace(on404))
                throw new InvalidOperationException(on404);
            throw new HttpRequestException($"Zoom query failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {SummarizeZoomError(body)}");
        }

        private async Task<int> ExtractAndSaveZoomTranscriptOrThrow(Meeting meeting, HttpClient http, string token, JsonDocument doc, CancellationToken ct)
        {
            var maybe = await TryExtractAndSaveZoomTranscript(meeting, http, token, doc, ct);
            if (maybe.HasValue) return maybe.Value;

            throw new InvalidOperationException(
                "No transcript file found in cloud recordings. " +
                "Enable 'Cloud recording' and 'Create audio transcript', record to cloud, and try again.");
        }

        private async Task<int?> TryExtractAndSaveZoomTranscript(Meeting meeting, HttpClient http, string token, JsonDocument doc, CancellationToken ct)
        {
            if (!doc.RootElement.TryGetProperty("recording_files", out var filesEl) || filesEl.ValueKind != JsonValueKind.Array)
                return null;

            var trFile = filesEl.EnumerateArray().FirstOrDefault(f =>
            {
                var type = f.TryGetProperty("file_type", out var t) ? t.GetString() : null;
                return type == "TRANSCRIPT" || type == "CC";
            });
            if (trFile.ValueKind == JsonValueKind.Undefined) return null;

            var fileId = trFile.GetProperty("id").GetString()!;
            var downloadUrl = trFile.GetProperty("download_url").GetString()!;
            var vttUrl = $"{downloadUrl}?access_token={token}";

            var vtt = await http.GetStringAsync(new Uri(vttUrl), ct);
            if (string.IsNullOrWhiteSpace(vtt))
                throw new InvalidOperationException("Zoom returned an empty transcript content.");

            return await SaveVtt(meeting, "Zoom", fileId, vtt, ct);
        }

        private static string SummarizeZoomError(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
                var root = doc.RootElement;
                var code = root.TryGetProperty("code", out var c) ? c.ToString() : "";
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                return string.IsNullOrEmpty(code) && string.IsNullOrEmpty(msg) ? json : $"code={code}, message={msg}";
            }
            catch { return json; }
        }

        // --------------------------------------------------------------------
        // Persist VTT → Transcript + TranscriptUtterance
        // --------------------------------------------------------------------
        private async Task<int> SaveVtt(Meeting meeting, string provider, string providerTranscriptId, string vtt, CancellationToken ct)
        {
            var existing = await _db.Set<Transcript>()
                .Include(t => t.Utterances)
                .FirstOrDefaultAsync(t => t.MeetingId == meeting.Id && t.Provider == provider, ct);

            if (existing != null)
            {
                _db.Set<TranscriptUtterance>().RemoveRange(
                    _db.Set<TranscriptUtterance>().Where(u => u.TranscriptId == existing.Id));

                existing.ProviderTranscriptId = providerTranscriptId;
                existing.CreatedUtc = DateTimeOffset.UtcNow;

                foreach (var cue in SimpleVtt.Parse(vtt))
                    existing.Utterances.Add(MapUtterance(meeting, existing, cue));

                await _db.SaveChangesAsync(ct);
                return existing.Utterances.Count;
            }

            var tr = new Transcript
            {
                MeetingId = meeting.Id,
                Provider = provider,
                ProviderTranscriptId = providerTranscriptId,
                CreatedUtc = DateTimeOffset.UtcNow
            };

            foreach (var cue in SimpleVtt.Parse(vtt))
                tr.Utterances.Add(MapUtterance(meeting, tr, cue));

            _db.Transcripts.Add(tr);
            await _db.SaveChangesAsync(ct);
            return tr.Utterances.Count;
        }

        private static TranscriptUtterance MapUtterance(Meeting meeting, Transcript tr, SimpleVtt.Cue cue)
        {
            string? userId = null;
            string? email = cue.SpeakerEmail;

            if (!string.IsNullOrWhiteSpace(email))
            {
                userId = meeting.Attendees
                    .FirstOrDefault(a => a.Email != null &&
                                         string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase))
                    ?.UserId;
            }

            if (userId is null && !string.IsNullOrWhiteSpace(cue.SpeakerName))
            {
                var byName = meeting.Attendees.FirstOrDefault(a =>
                    !string.IsNullOrWhiteSpace(a.Name) &&
                    string.Equals(a.Name, cue.SpeakerName, StringComparison.OrdinalIgnoreCase));
                userId ??= byName?.UserId;
                email ??= byName?.Email;
            }

            return new TranscriptUtterance
            {
                Transcript = tr,
                Start = cue.Start,
                End = cue.End,
                Text = cue.Text,
                SpeakerName = cue.SpeakerName,
                SpeakerEmail = email,
                UserId = userId
            };
        }

        // ---------------------------
        // Email the transcript (SMTP-friendly via IEmailSender)
        // ---------------------------
        private async Task EmailTranscriptAsync(Meeting meeting, CancellationToken ct)
        {
            var transcript = await _db.Set<Transcript>()
                .AsNoTracking()
                .Include(t => t.Utterances)
                .Where(t => t.MeetingId == meeting.Id)
                .OrderByDescending(t => t.CreatedUtc)
                .FirstOrDefaultAsync(ct);

            if (transcript is null) return;

            var lines = transcript.Utterances
                .OrderBy(u => u.Start)
                .Take(10)
                .Select(u =>
                {
                    var speaker = string.IsNullOrWhiteSpace(u.SpeakerName) ? (u.SpeakerEmail ?? "Unknown") : u.SpeakerName;
                    return $"<li><strong>{System.Net.WebUtility.HtmlEncode(speaker)}</strong>: {System.Net.WebUtility.HtmlEncode(u.Text)}</li>";
                });

            var meetingUrl = string.IsNullOrWhiteSpace(_app.AppBaseUrl)
                ? null
                : $"{_app.AppBaseUrl!.TrimEnd('/')}/meetings/{meeting.Id}/transcripts";

            var html = new StringBuilder()
                .Append("<!DOCTYPE html>")
                .Append("<html lang='en'>")
                .Append("<head>")
                .Append("<meta charset='UTF-8'>")
                .Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>")
                .Append("<style>")
                .Append("body { font-family: Arial, sans-serif; background-color: #f4f6f8; margin:0; padding:20px; }")
                .Append(".container { max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); padding:20px; }")
                .Append(".header { border-bottom: 2px solid #0078d4; padding-bottom: 10px; margin-bottom: 20px; }")
                .Append(".header h2 { margin:0; color:#0078d4; font-size:20px; }")
                .Append(".meta { font-size:14px; color:#555; margin-bottom: 20px; }")
                .Append(".preview { margin: 20px 0; }")
                .Append(".preview ol { padding-left:20px; }")
                .Append(".preview li { margin-bottom:8px; line-height:1.4; }")
                .Append(".footer { margin-top:30px; font-size:12px; color:#999; text-align:center; }")
                .Append(".btn { display:inline-block; padding:10px 15px; margin-top:15px; background:#0078d4; color:#fff; text-decoration:none; border-radius:4px; }")
                .Append("</style>")
                .Append("</head>")
                .Append("<body>")
                .Append("<div class='container'>")
                .Append("<div class='header'><h2>📄 Meeting Transcript</h2></div>")
                .Append("<div class='meta'>")
                .Append($"<p><strong>Title:</strong> {System.Net.WebUtility.HtmlEncode(meeting.Title)}</p>")
                .Append($"<p><strong>When:</strong> {meeting.ScheduledAt.LocalDateTime:G}</p>")
                .Append("</div>")
                .Append("<p>Hello,</p>")
                .Append("<p>The transcript for your meeting has been ingested successfully. Below is a short preview:</p>")
                .Append("<div class='preview'><ol>")
                .Append(string.Join("", lines))
                .Append("</ol></div>");

            if (!string.IsNullOrWhiteSpace(meetingUrl))
                html.Append($"<p><a href='{meetingUrl}' class='btn'>View Full Transcript</a></p>");

            html.Append("<div class='footer'>")
                .Append("<p>You are receiving this email because you attended this meeting.</p>")
                .Append("<p>&copy; BoardMgmt</p>")
                .Append("</div>")
                .Append("</div>")
                .Append("</body>")
                .Append("</html>");

            var vttBytes = BuildVttFromUtterances(transcript);
            var attachment = ("transcript.vtt", "text/vtt", vttBytes);

            var recipients = meeting.Attendees
                .Select(a => a.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0) return;

            var subject = $"Transcript: {meeting.Title} ({meeting.ScheduledAt:yyyy-MM-dd})";

            var from = !string.IsNullOrWhiteSpace(meeting.ExternalCalendarMailbox)
                ? meeting.ExternalCalendarMailbox!
                : (_app.MailboxAddress ?? throw new InvalidOperationException("No sender mailbox configured (App:MailboxAddress)."));

            await _email.SendAsync(
                from,
                recipients,
                subject,
                html.ToString(),
                attachment,
                ct);
        }

        private static byte[] BuildVttFromUtterances(Transcript transcript)
        {
            var sb = new StringBuilder();
            sb.AppendLine("WEBVTT");
            sb.AppendLine();

            foreach (var u in transcript.Utterances.OrderBy(x => x.Start))
            {
                static string fmt(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000}";
                sb.AppendLine($"{fmt(u.Start)} --> {fmt(u.End)}");
                var speaker = string.IsNullOrWhiteSpace(u.SpeakerName) ? u.SpeakerEmail : u.SpeakerName;
                if (!string.IsNullOrWhiteSpace(speaker))
                    sb.AppendLine($"{speaker}: {u.Text}");
                else
                    sb.AppendLine(u.Text);
                sb.AppendLine();
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}