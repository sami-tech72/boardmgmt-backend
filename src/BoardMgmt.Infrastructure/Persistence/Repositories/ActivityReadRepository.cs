using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Dashboard.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Persistence.Repositories;

public sealed class ActivityReadRepository : IActivityReadRepository
{
    private readonly AppDbContext _db;
    public ActivityReadRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DashboardActivityDto>> GetRecentAsync(int take, CancellationToken ct)
    {
        // Build EF-translatable projections with only primitive values
        var meetings = _db.Meetings.AsNoTracking()
            .Select(m => new
            {
                Id = m.Id,
                Kind = "meeting",
                Title = "Meeting scheduled",
                Text = m.Title,
                When = m.ScheduledAt,                 // DateTimeOffset
                Color = "info"
            });

        var documents = _db.Documents.AsNoTracking()
            .Select(d => new
            {
                Id = d.Id,
                Kind = "document",
                Title = "Document uploaded",
                Text = d.OriginalName,
                When = d.UploadedAt,                  // DateTimeOffset
                Color = "success"
            });

        var votes = _db.VotePolls.AsNoTracking()
            .Select(v => new
            {
                Id = v.Id,
                Kind = "vote",
                Title = "Vote opened",
                Text = v.Title,
                When = v.CreatedAt,                   // DateTimeOffset
                Color = "warning"
            });

        // Chat messages (avoid custom methods in Select)
        var chat = _db.ChatMessages.AsNoTracking()
            .Where(m => m.DeletedAtUtc == null)
            .Select(m => new
            {
                Id = m.Id,
                Kind = m.ThreadRootId == null ? "message" : "reply",
                Title = m.ThreadRootId == null ? "New message" : "New reply",
                Text = m.BodyHtml,                    // raw HTML for now
                When = (DateTimeOffset)m.CreatedAtUtc, // normalize to DateTimeOffset for union
                Color = "primary"
            });

        var meetingsCompleted = _db.Meetings.AsNoTracking()
            .Where(m => m.EndAt != null)
            .Select(m => new
            {
                Id = m.Id,
                Kind = "meeting_completed",
                Title = "Meeting completed",
                Text = m.Title,
                When = m.EndAt!.Value,                // DateTimeOffset
                Color = "secondary"
            });

        // Union on a common anonymous shape → order → take → materialize
        var raw = await meetings
            .Concat(documents)
            .Concat(votes)
            .Concat(chat)
            .Concat(meetingsCompleted)
            .OrderByDescending(x => x.When)
            .Take(take)
            .ToListAsync(ct);

        // Now do non-translatable work in memory
        var now = DateTime.UtcNow;
        var result = raw.Select(x =>
        {
            var whenUtc = x.When.UtcDateTime;
            var plain = HtmlToPlain(x.Text);
            var snippet = Truncate(plain, 120);

            // NOTE: positional args (no named arguments)
            return new DashboardActivityDto(
                x.Id,
                x.Kind,
                x.Title,
                snippet,
                WhenAgo(now, whenUtc),
                x.Color
            );
        }).ToList();

        return result;
    }

    // --- helpers (run in memory only) ---

    private static string WhenAgo(DateTime nowUtc, DateTime atUtc)
    {
        var span = nowUtc - atUtc;
        if (span.TotalMinutes < 90) return $"{Math.Max(1, (int)span.TotalMinutes)} minutes ago";
        if (span.TotalHours < 36) return $"{Math.Max(1, (int)span.TotalHours)} hours ago";
        return $"{Math.Max(1, (int)span.TotalDays)} days ago";
    }

    private static string HtmlToPlain(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
        return System.Text.RegularExpressions.Regex.Replace(noTags, "\\s+", " ").Trim();
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
