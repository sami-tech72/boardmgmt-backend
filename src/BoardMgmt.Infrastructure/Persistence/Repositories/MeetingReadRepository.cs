using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Dashboard.DTOs;
using BoardMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Persistence.Repositories;

public class MeetingReadRepository : IMeetingReadRepository
{
    private readonly DbContext _db;
    public MeetingReadRepository(DbContext db) => _db = db;

    public Task<int> CountUpcomingAsync(DateTime utcNow, CancellationToken ct) =>
        _db.Set<Meeting>()
           .Where(m =>
               m.Status == MeetingStatus.Scheduled &&
               m.ScheduledAt >= DateTime.SpecifyKind(utcNow, DateTimeKind.Utc))
           .CountAsync(ct);

    public async Task<IReadOnlyList<DashboardMeetingDto>> GetRecentAsync(int take, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;

        var data = await _db.Set<Meeting>()
            .OrderByDescending(m => m.ScheduledAt)
            .Take(take)
            .Select(m => new DashboardMeetingDto(
                m.Id,
                m.Title,
                m.Location, // no Subtitle on entity; Location is a reasonable stand-in
                m.ScheduledAt.UtcDateTime, // DateTimeOffset -> DateTime (UTC)
                                           // Map "Scheduled" in the future to "Upcoming", otherwise use enum text
                m.Status == MeetingStatus.Scheduled && m.ScheduledAt.UtcDateTime > nowUtc
                    ? "Upcoming"
                    : m.Status.ToString()))
            .ToListAsync(ct);

        return data;
    }
}
