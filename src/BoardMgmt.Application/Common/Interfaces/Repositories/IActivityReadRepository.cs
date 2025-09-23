using BoardMgmt.Application.Dashboard.DTOs;

namespace BoardMgmt.Application.Common.Interfaces.Repositories;

public interface IActivityReadRepository
{
    Task<IReadOnlyList<DashboardActivityDto>> GetRecentAsync(int take, CancellationToken ct);
}
