using BoardMgmt.Application.Dashboard.DTOs;

namespace BoardMgmt.Application.Common.Interfaces.Repositories;

public interface IDocumentReadRepository
{
    Task<int> CountActiveAsync(CancellationToken ct);
    Task<IReadOnlyList<DashboardDocumentDto>> GetRecentAsync(int take, CancellationToken ct);
}
