using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Reports.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Reports.Queries;

public record GetReportsDashboardQuery(int Months = 6) : IRequest<ReportsDashboardDto>;

public sealed class GetReportsDashboardHandler : IRequestHandler<GetReportsDashboardQuery, ReportsDashboardDto>
{
    private readonly IAppDbContext _db;

    public GetReportsDashboardHandler(IAppDbContext db) => _db = db;

    public async Task<ReportsDashboardDto> Handle(GetReportsDashboardQuery request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddMonths(-Math.Max(1, request.Months) + 1); // inclusive month window

        // Helper: bucket by year-month
        static string ym(DateTimeOffset dt) => $"{dt:yyyy-MM}";

        // Meetings in range (by ScheduledAt month)
        var meetings = await _db.Set<Meeting>()
            .AsNoTracking()
            .Where(m => m.ScheduledAt >= new DateTimeOffset(start.Year, start.Month, 1, 0, 0, 0, TimeSpan.Zero))
            .ToListAsync(ct);

        // Attendees (confirmed) by meeting
        var meetingIds = meetings.Select(m => m.Id).ToList();
        var attendees = await _db.Set<MeetingAttendee>()
            .AsNoTracking()
            .Where(a => meetingIds.Contains(a.MeetingId))
            .ToListAsync(ct);

        var agendas = await _db.Set<AgendaItem>()
            .AsNoTracking()
            .Where(a => meetingIds.Contains(a.MeetingId))
            .ToListAsync(ct);

        var polls = await _db.Set<VotePoll>()
            .AsNoTracking()
            .Where(v => v.CreatedAt >= new DateTimeOffset(start.Year, start.Month, 1, 0, 0, 0, TimeSpan.Zero))
            .ToListAsync(ct);

        var pollIds = polls.Select(p => p.Id).ToList();
        var ballots = await _db.Set<VoteBallot>()
            .AsNoTracking()
            .Where(b => pollIds.Contains(b.VoteId))
            .ToListAsync(ct);

        var docs = await _db.Set<Document>()
            .AsNoTracking()
            .Where(d => d.UploadedAt >= new DateTimeOffset(start.Year, start.Month, 1, 0, 0, 0, TimeSpan.Zero))
            .ToListAsync(ct);

        // Build month buckets in order
        var months = Enumerable
            .Range(0, request.Months)
            .Select(i => start.AddMonths(i))
            .Select(d => (Key: ym(d), Year: d.Year, Month: d.Month))
            .ToList();

        // Attendance series
        var attendance = months.Select(mb =>
        {
            var ms = meetings.Where(x => x.ScheduledAt.Year == mb.Year && x.ScheduledAt.Month == mb.Month).ToList();
            var mIds = ms.Select(x => x.Id).ToHashSet();
            var attCnt = attendees.Where(a => mIds.Contains(a.MeetingId) && a.IsConfirmed).Count();
            return new AttendancePoint(mb.Key, ms.Count, attCnt);
        }).ToList();

        // Voting series (+ participation rate)
        var voting = months.Select(mb =>
        {
            var ps = polls.Where(p => p.CreatedAt.Year == mb.Year && p.CreatedAt.Month == mb.Month).ToList();
            var pIds = ps.Select(x => x.Id).ToHashSet();
            var bs = ballots.Where(b => pIds.Contains(b.VoteId)).ToList();

            // naive denominator: unique voters / estimated eligible
            // if VoteEligibility == MeetingAttendees and poll has MeetingId, use that count; else fallback to ballots count
            int eligible = 0;
            foreach (var p in ps)
            {
                if (p.Eligibility == VoteEligibility.MeetingAttendees && p.MeetingId.HasValue)
                {
                    eligible += attendees.Count(a => a.MeetingId == p.MeetingId.Value);
                }
                else
                {
                    // no explicit eligible list stored; use distinct ballot users as proxy
                    eligible += bs.Where(b => b.VoteId == p.Id).Select(b => b.UserId).Distinct().Count();
                }
            }

            double rate = eligible > 0 ? (100.0 * bs.Select(b => (b.UserId, b.VoteId)).Distinct().Count() / eligible) : 0.0;
            return new VotingTrendPoint(mb.Key, ps.Count, bs.Count, Math.Round(rate, 1));
        }).ToList();

        // Documents series
        var documents = months.Select(mb =>
        {
            var ds = docs.Where(d => d.UploadedAt.Year == mb.Year && d.UploadedAt.Month == mb.Month).ToList();
            return new DocumentUsagePoint(mb.Key, ds.Count, ds.Sum(d => d.SizeBytes));
        }).ToList();

        // Performance metrics (aggregate over window)
        var meetingsScheduled = meetings.Count;
        var meetingsCompleted = meetings.Count(m => m.Status == MeetingStatus.Completed);
        var avgAgendaItems = meetingsScheduled > 0 ? (double)agendas.Count / meetingsScheduled : 0;
        var avgDocsPerMeeting = meetingsScheduled > 0 ? (double)docs.Count(d => d.MeetingId != null) / meetingsScheduled : 0;
        var avgAttendeesPerMtg = meetingsScheduled > 0 ? (double)attendees.Count / meetingsScheduled : 0;

        // polls per meeting
        var pollsPerMeeting = meetingsScheduled > 0 ? (double)polls.Count / meetingsScheduled : 0;

        var perf = new PerformanceMetricsDto(
            meetingsScheduled,
            meetingsCompleted,
            Math.Round(avgAgendaItems, 2),
            Math.Round(avgDocsPerMeeting, 2),
            Math.Round(avgAttendeesPerMtg, 2),
            Math.Round(pollsPerMeeting, 2)
        );

        // Recent generated reports
        var recent = await _db.Set<GeneratedReport>()
            .AsNoTracking()
            .OrderByDescending(x => x.GeneratedAt)
            .Take(10)
            .Select(x => new RecentReportDto(
                x.Id, x.Name, x.Type,
                x.GeneratedByUser!.DisplayName ?? x.GeneratedByUser!.UserName ?? "—",
                x.GeneratedAt, x.FileUrl, x.Format, x.PeriodLabel
            ))
            .ToListAsync(ct);

        return new ReportsDashboardDto
        {
            Attendance = attendance,
            Voting = voting,
            Documents = documents,
            Performance = perf,
            Recent = recent
        };
    }
}
