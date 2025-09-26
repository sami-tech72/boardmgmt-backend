using BoardMgmt.Application.Dashboard.DTOs;

namespace BoardMgmt.Application.Common.Interfaces.Repositories;

public interface IMeetingReadRepository
{
    Task<int> CountUpcomingAsync(DateTime utcNow, CancellationToken ct);

   
    Task<IReadOnlyList<DashboardMeetingDto>> GetRecentAsync(int take, CancellationToken ct);

    // NEW: detail list of upcoming meetings (the stat)
    Task<(int total, IReadOnlyList<MeetingItemDto> items)> GetUpcomingPagedAsync(int page, int pageSize, CancellationToken ct);
}
