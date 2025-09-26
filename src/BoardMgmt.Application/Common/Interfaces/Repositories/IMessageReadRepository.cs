using BoardMgmt.Application.Dashboard.DTOs;

namespace BoardMgmt.Application.Common.Interfaces.Repositories;

public interface IMessageReadRepository
{
    Task<int> CountUnreadAsync(Guid? userId, CancellationToken ct); // pass null if not scoping to user

    // NEW: detail list for unread messages
    Task<(int total, IReadOnlyList<UnreadMessageItemDto> items)> GetUnreadPagedAsync(Guid? userId, int page, int pageSize, CancellationToken ct);
}
