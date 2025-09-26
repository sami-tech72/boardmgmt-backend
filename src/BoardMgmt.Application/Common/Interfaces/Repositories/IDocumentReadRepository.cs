using BoardMgmt.Application.Dashboard.DTOs;

namespace BoardMgmt.Application.Common.Interfaces.Repositories;

public interface IDocumentReadRepository
{
    Task<int> CountActiveAsync(CancellationToken ct);
    Task<IReadOnlyList<DashboardDocumentDto>> GetRecentAsync(int take, CancellationToken ct);


    // NEW: detail list for active documents
    Task<(int total, IReadOnlyList<DocumentItemDto> items)> GetActivePagedAsync(int page, int pageSize, CancellationToken ct);
}
