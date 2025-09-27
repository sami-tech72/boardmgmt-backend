// Application/Common/Interfaces/Repositories/IUserReadRepository.cs
using BoardMgmt.Application.Dashboard.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace BoardMgmt.Application.Common.Interfaces.Repositories;

public interface IUserReadRepository
{
    /// <summary>
    /// Count of “active” users.
    /// Default definition: enabled (not locked/disabled/deleted).
    /// If you maintain a boolean IsActive, it will be used when present.
    /// </summary>
    Task<int> CountActiveAsync(CancellationToken ct);

    Task<(int total, IReadOnlyList<ActiveUserItemDto> items)>
        GetActivePagedAsync(int page, int pageSize, CancellationToken ct);
}
