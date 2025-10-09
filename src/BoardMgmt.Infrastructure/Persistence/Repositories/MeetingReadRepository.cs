using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Dashboard.DTOs;
using BoardMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Infrastructure.Persistence;

namespace BoardMgmt.Infrastructure.Persistence.Repositories;

public class MeetingReadRepository : IMeetingReadRepository
{
    private readonly AppDbContext _db;
    public MeetingReadRepository(AppDbContext db) => _db = db;

    public Task<int> CountUpcomingAsync(DateTime utcNow, CancellationToken ct)
    {
        // Convert incoming DateTime to a DateTimeOffset in UTC for SQL-side comparison
        var nowOffset = utcNow.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(utcNow)
            : new DateTimeOffset(utcNow.ToUniversalTime());

        return _db.Set<Meeting>()
            .Where(m =>
                m.Status == MeetingStatus.Scheduled &&        // ✅ compare enum directly
                m.ScheduledAt >= nowOffset)                   // ✅ use DateTimeOffset (no .UtcDateTime)
            .CountAsync(ct);
    }

    public async Task<IReadOnlyList<DashboardMeetingDto>> GetRecentAsync(int take, CancellationToken ct)
    {
        var nowOffset = DateTimeOffset.UtcNow;

        // First project raw columns EF can translate, then convert after materialization
        var rows = await _db.Set<Meeting>()
            .OrderByDescending(m => m.ScheduledAt)
            .Take(take)
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.Location,
                m.ScheduledAt,
                m.Status
            })
            .ToListAsync(ct);

        var data = rows
            .Select(m => new DashboardMeetingDto(
                m.Id,
                m.Title,
                m.Location,
                m.ScheduledAt.UtcDateTime, // ✅ safe now (in memory)
                (m.Status == MeetingStatus.Scheduled && m.ScheduledAt > nowOffset)
                    ? "Upcoming"
                    : m.Status.ToString()))
            .ToList();

        return data;
    }

    public async Task<(int total, IReadOnlyList<MeetingItemDto> items)> GetUpcomingPagedAsync(int page, int pageSize, CancellationToken ct)
    {
        var nowOffset = DateTimeOffset.UtcNow;

        var baseQuery = _db.Set<Meeting>()
            .Where(m =>
                m.Status == MeetingStatus.Scheduled &&        // ✅ no cast
                m.ScheduledAt >= nowOffset);                  // ✅ no .UtcDateTime in WHERE

        var total = await baseQuery.CountAsync(ct);

        var pageRows = await baseQuery
            .OrderBy(m => m.ScheduledAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.Location,
                m.ScheduledAt
            })
            .ToListAsync(ct);

        var items = pageRows
            .Select(m => new MeetingItemDto(
                m.Id,
                m.Title,
                m.Location,
                m.ScheduledAt.UtcDateTime, // ✅ convert after materialization
                "Upcoming"))
            .ToList();

        return (total, items);
    }
}
