// File: Application/Meetings/Commands/IngestTranscriptHandler.cs
using System;
using System.Collections.Generic;
using System.Data; // IsolationLevel
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Common.Email;
using BoardMgmt.Application.Common.Options;
using BoardMgmt.Application.Common.Parsing; // SimpleVtt
using BoardMgmt.Domain.Calendars;
using BoardMgmt.Domain.Entities;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage; // CreateExecutionStrategy
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace BoardMgmt.Application.Meetings.Commands
{
    // Provider-specific ingestion lives in partials:
    //   - IngestTranscriptHandler.Teams.cs  (Microsoft 365 / Teams)
    //   - IngestTranscriptHandler.Zoom.cs   (Zoom)
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

        public async Task<int> Handle(IngestTranscriptCommand request, CancellationToken ct)
        {
            var meeting = await _db.Set<Meeting>()
                .Include(m => m.Attendees)
                .FirstOrDefaultAsync(m => m.Id == request.MeetingId, ct)
                ?? throw new InvalidOperationException("Meeting not found.");

            if (string.IsNullOrWhiteSpace(meeting.ExternalCalendar))
                throw new InvalidOperationException("Meeting.ExternalCalendar not set.");

            var provider = CalendarProviders.Normalize(meeting.ExternalCalendar);

            if (!CalendarProviders.IsSupported(provider))
                throw new InvalidOperationException($"Unsupported provider: {meeting.ExternalCalendar}");

            if (meeting.ExternalCalendar != provider)
                meeting.ExternalCalendar = provider;

            if (string.IsNullOrWhiteSpace(meeting.ExternalEventId))
                throw new InvalidOperationException("Meeting.ExternalEventId not set.");

            var count = provider switch
            {
                CalendarProviders.Microsoft365 => await IngestTeams(meeting, ct),
                CalendarProviders.Zoom => await IngestZoom(meeting, ct),
                _ => throw new InvalidOperationException($"Unsupported provider: {meeting.ExternalCalendar}")
            };

            await EmailTranscriptAsync(meeting, ct);
            return count;
        }

        // --------------------------------------------------------------------
        // Persist VTT → Transcript + TranscriptUtterance (idempotent replace)
        // --------------------------------------------------------------------
        public async Task<int> SaveVtt(Meeting meeting, string provider, string providerTranscriptId, string vtt, CancellationToken ct)
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

        private async Task<int> SaveVttWithRetryAsync(
            Meeting meeting,
            string provider,
            string providerTranscriptId,
            IReadOnlyList<SimpleVtt.Cue> cues,
            int attempt,
            int maxAttempts,
            CancellationToken ct)
        {
            if (_db is not DbContext db)
                throw new InvalidOperationException("SaveVtt requires DbContext.");

            if (attempt > 1) db.ChangeTracker.Clear();

            // Wrap the WHOLE transactional unit in the SQL Server execution strategy,
            // so transient faults can retry atomically (includes user-initiated transaction).
            var strategy = db.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    // This inner loop is only for optimistic concurrency conflicts (not transient SQL errors).
                    var localAttempt = attempt;
                    while (true)
                    {
                        if (localAttempt > 1) db.ChangeTracker.Clear();

                        try
                        {
                            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                            // Get or create Transcript (unique on MeetingId+Provider)
                            var transcript = await db.Set<Transcript>()
                                .SingleOrDefaultAsync(t => t.MeetingId == meeting.Id && t.Provider == provider, ct);

                            if (transcript is null)
                            {
                                transcript = new Transcript
                                {
                                    MeetingId = meeting.Id,
                                    Provider = provider,
                                    ProviderTranscriptId = providerTranscriptId,
                                    CreatedUtc = DateTimeOffset.UtcNow
                                };
                                db.Set<Transcript>().Add(transcript);
                                await db.SaveChangesAsync(ct); // ensure Id
                            }
                            else
                            {
                                // Keep CreatedUtc stable; only update external id
                                transcript.ProviderTranscriptId = providerTranscriptId;
                                await db.SaveChangesAsync(ct);
                            }

                            var transcriptId = transcript.Id;

                            // Replace utterances set-based (no tracked collection)
                            await db.Set<TranscriptUtterance>()
                                .Where(u => u.TranscriptId == transcriptId)
                                .ExecuteDeleteAsync(ct);

                            var toInsert = new List<TranscriptUtterance>(cues.Count);
                            foreach (var cue in cues)
                                toInsert.Add(MapUtterance(meeting, transcriptId, cue));

                            if (toInsert.Count > 0)
                            {
                                await db.Set<TranscriptUtterance>().AddRangeAsync(toInsert, ct);
                                await db.SaveChangesAsync(ct);
                            }

                            await tx.CommitAsync(ct);
                            return toInsert.Count;
                        }
                        catch (DbUpdateConcurrencyException ex)
                        {
                            if (localAttempt >= maxAttempts)
                            {
                                _logger.LogError(ex,
                                    "Failed to save transcript {ProviderTranscriptId} for meeting {MeetingId} ({Provider}) after {MaxAttempts} attempts due to concurrency conflicts.",
                                    providerTranscriptId, meeting.Id, provider, maxAttempts);

                                throw new InvalidOperationException("Failed to save transcript after retrying due to concurrency conflicts.", ex);
                            }

                            var backoff = TimeSpan.FromMilliseconds(Math.Pow(2, localAttempt) * 50);
                            try { await Task.Delay(backoff, ct); } catch { /* ignore cancellation */ }
                            localAttempt++;
                            // loop and retry inside the same execution-strategy attempt
                        }
                    }
                });
            }
            catch (DbUpdateException ex)
            {
                // Covers unique index collisions, FK issues, etc.
                _logger.LogError(ex,
                    "Write error while saving transcript {ProviderTranscriptId} for meeting {MeetingId} ({Provider}).",
                    providerTranscriptId, meeting.Id, provider);
                throw;
            }
        }

        private async Task DeleteExistingUtterancesAsync(Transcript transcript, CancellationToken ct)
        {
            if (_db is not DbContext db) return;

            await db.Set<TranscriptUtterance>()
                .Where(u => u.TranscriptId == transcript.Id)
                .ExecuteDeleteAsync(ct);
            // IMPORTANT: don't touch transcript.Utterances collection here.
        }

        private const int MaxUtteranceTextLength = 4000;

        private static TranscriptUtterance MapUtterance(Meeting meeting, Guid transcriptId, SimpleVtt.Cue cue)
        {
            string? userId = null;
            string? email = cue.SpeakerEmail;

            var text = cue.Text;
            if (text.Length > MaxUtteranceTextLength)
            {
                const string ellipsis = "…";
                var max = Math.Max(0, MaxUtteranceTextLength - ellipsis.Length);
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
                TranscriptId = transcriptId,
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
                    return $"<li><strong>{WebUtility.HtmlEncode(speaker)}</strong>: {WebUtility.HtmlEncode(u.Text)}</li>";
                });

            var meetingUrl = string.IsNullOrWhiteSpace(_app.AppBaseUrl)
                ? null
                : $"{_app.AppBaseUrl!.TrimEnd('/')}/meetings/{meeting.Id}/transcripts";

            var html = new StringBuilder()
                .Append("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'>")
                .Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>")
                .Append("<style>")
                .Append("body{font-family:Arial,sans-serif;background:#f4f6f8;margin:0;padding:20px}")
                .Append(".container{max-width:600px;margin:0 auto;background:#fff;border-radius:8px;box-shadow:0 2px 4px rgba(0,0,0,.1);padding:20px}")
                .Append(".header{border-bottom:2px solid #0078d4;padding-bottom:10px;margin-bottom:20px}")
                .Append(".header h2{margin:0;color:#0078d4;font-size:20px}")
                .Append(".meta{font-size:14px;color:#555;margin-bottom:20px}")
                .Append(".preview{margin:20px 0}.preview ol{padding-left:20px}.preview li{margin-bottom:8px;line-height:1.4}")
                .Append(".footer{margin-top:30px;font-size:12px;color:#999;text-align:center}")
                .Append(".btn{display:inline-block;padding:10px 15px;margin-top:15px;background:#0078d4;color:#fff;text-decoration:none;border-radius:4px}")
                .Append("</style></head><body><div class='container'>")
                .Append("<div class='header'><h2>📄 Meeting Transcript</h2></div>")
                .Append("<div class='meta'>")
                .Append($"<p><strong>Title:</strong> {WebUtility.HtmlEncode(meeting.Title)}</p>")
                .Append($"<p><strong>When:</strong> {meeting.ScheduledAt.LocalDateTime:G}</p>")
                .Append("</div>")
                .Append("<p>Hello,</p><p>The transcript for your meeting has been ingested successfully. Below is a short preview:</p>")
                .Append("<div class='preview'><ol>")
                .Append(string.Join("", lines))
                .Append("</ol></div>");

            if (!string.IsNullOrWhiteSpace(meetingUrl))
                html.Append($"<p><a href='{meetingUrl}' class='btn'>View Full Transcript</a></p>");

            html.Append("<div class='footer'><p>You are receiving this email because you attended this meeting.</p>")
                .Append("<p>&copy; BoardMgmt</p></div></div></body></html>");

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
            static string Fmt(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000}";
            var sb = new StringBuilder();
            sb.AppendLine("WEBVTT").AppendLine();

            foreach (var u in transcript.Utterances.OrderBy(x => x.Start))
            {
                sb.AppendLine($"{Fmt(u.Start)} --> {Fmt(u.End)}");
                var speaker = string.IsNullOrWhiteSpace(u.SpeakerName) ? u.SpeakerEmail : u.SpeakerName;
                sb.AppendLine(!string.IsNullOrWhiteSpace(speaker) ? $"{speaker}: {u.Text}" : u.Text);
                sb.AppendLine();
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
