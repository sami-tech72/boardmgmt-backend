using BoardMgmt.Application.Dashboard.DTOs;

namespace BoardMgmt.Application.Common.Interfaces.Repositories;

public interface IMeetingReadRepository
{
    Task<int> CountUpcomingAsync(DateTime utcNow, CancellationToken ct);
    Task<IReadOnlyList<DashboardMeetingDto>> GetRecentAsync(int take, CancellationToken ct);
}
