using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace BoardMgmt.Application.Meetings.Commands
{
    public sealed partial class IngestTranscriptHandler(
        IAppDbContext db,
        GraphServiceClient graph,
        IHttpClientFactory httpFactory,
        IZoomTokenProvider zoomTokenProvider,
        IEmailSender email,
        IOptions<AppOptions> app,
        ILogger<IngestTranscriptHandler> logger)
        : IRequestHandler<IngestTranscriptCommand, int>
    {
        private readonly IAppDbContext _db = db;
        private readonly GraphServiceClient _graph = graph;
        private readonly IHttpClientFactory _httpFactory = httpFactory;
        private readonly IZoomTokenProvider _zoomTokenProvider = zoomTokenProvider;
        private readonly IEmailSender _email = email;
        private readonly AppOptions _app = app.Value ?? new AppOptions();
        private readonly ILogger<IngestTranscriptHandler> _logger = logger;

        // Provider-specific ingestion workflows live in the partial class files next to this one:
        //   • IngestTranscriptHandler.Teams.cs → Microsoft 365 / Teams
        //   • IngestTranscriptHandler.Zoom.cs  → Zoom
        // Common persistence and notification helpers remain in this file.

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
        // Persist VTT → Transcript + TranscriptUtterance
        // --------------------------------------------------------------------
        private async Task<int> SaveVtt(Meeting meeting, string provider, string providerTranscriptId, string vtt, CancellationToken ct)
        {
            var cues = SimpleVtt.Parse(vtt).ToList();
            const int MaxAttempts = 3;

            return await SaveVttWithRetryAsync(
                meeting,
                provider,
                providerTranscriptId,
                cues,
                attempt: 1,
                maxAttempts: MaxAttempts,
                ct);
        }

        // ✅ CancellationToken moved to the end; call sites updated
        private async Task<int> SaveVttWithRetryAsync(
            Meeting meeting,
            string provider,
            string providerTranscriptId,
            IReadOnlyList<SimpleVtt.Cue> cues,
            int attempt,
            int maxAttempts,
            CancellationToken ct)
        {
            if (attempt > 1 && _db is DbContext dbContext)
            {
                dbContext.ChangeTracker.Clear();
            }

            try
            {
                var existing = await _db.Set<Transcript>()
                    .Include(t => t.Utterances)
                    .FirstOrDefaultAsync(t => t.MeetingId == meeting.Id && t.Provider == provider, ct);

                if (existing != null)
                {
                    if (existing.Utterances.Count > 0)
                    {
                        await DeleteExistingUtterancesAsync(existing, ct);
                    }

                    existing.ProviderTranscriptId = providerTranscriptId;
                    existing.CreatedUtc = DateTimeOffset.UtcNow;

                    foreach (var cue in cues)
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

                foreach (var cue in cues)
                    tr.Utterances.Add(MapUtterance(meeting, tr, cue));

                _db.Transcripts.Add(tr);
                await _db.SaveChangesAsync(ct);
                return tr.Utterances.Count;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (_db is DbContext retryContext)
                {
                    retryContext.ChangeTracker.Clear();
                }

                if (attempt < maxAttempts)
                {
                    var backoffDelay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 50);
                    try
                    {
                        await Task.Delay(backoffDelay, ct);
                    }
                    catch (TaskCanceledException)
                    {
                        // Ignore cancellation during delay so that we can surface the original concurrency exception below.
                    }
                }

                if (attempt >= maxAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Failed to save transcript {ProviderTranscriptId} for meeting {MeetingId} using provider {Provider} after {MaxAttempts} attempts due to concurrency conflicts.",
                        providerTranscriptId,
                        meeting.Id,
                        provider,
                        maxAttempts);

                    throw new InvalidOperationException("Failed to save transcript after retrying due to concurrency conflicts.", ex);
                }

                _logger.LogWarning(
                    ex,
                    "Concurrency conflict while saving transcript {ProviderTranscriptId} for meeting {MeetingId} using provider {Provider}. Retrying attempt {Attempt} of {MaxAttempts}.",
                    providerTranscriptId,
                    meeting.Id,
                    provider,
                    attempt,
                    maxAttempts);

                return await SaveVttWithRetryAsync(
                    meeting,
                    provider,
                    providerTranscriptId,
                    cues,
                    attempt + 1,
                    maxAttempts,
                    ct);
            }
        }

        private async Task DeleteExistingUtterancesAsync(Transcript transcript, CancellationToken ct)
        {
            if (_db is DbContext dbContext)
            {
                var utterances = await dbContext.Set<TranscriptUtterance>()
                    .Where(u => u.TranscriptId == transcript.Id)
                    .ToListAsync(ct);

                if (utterances.Count > 0)
                {
                    dbContext.Set<TranscriptUtterance>().RemoveRange(utterances);
                }

                foreach (var entry in dbContext.ChangeTracker.Entries<TranscriptUtterance>()
                             .Where(e => e.Entity.TranscriptId == transcript.Id))
                {
                    entry.State = EntityState.Detached;
                }
            }
            else
            {
                _db.Set<TranscriptUtterance>().RemoveRange(transcript.Utterances);
            }

            transcript.Utterances.Clear();
        }

        private const int MaxUtteranceTextLength = 4000;

        private static TranscriptUtterance MapUtterance(Meeting meeting, Transcript tr, SimpleVtt.Cue cue)
        {
            string? userId = null;
            string? email = cue.SpeakerEmail;

            var text = cue.Text;
            if (text.Length > MaxUtteranceTextLength)
            {
                // Avoid exceeding the database limit imposed by TranscriptUtterance.Text (nvarchar(4000)).
                // Keep as much of the speaker text as possible and add an ellipsis to indicate truncation.
                const string ellipsis = "…";
                var max = MaxUtteranceTextLength - ellipsis.Length;
                if (max < 0) max = 0;
                text = text[..max] + ellipsis;
            }

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
                Text = text,
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