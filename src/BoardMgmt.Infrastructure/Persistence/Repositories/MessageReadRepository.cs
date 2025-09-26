using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Dashboard.DTOs;
using BoardMgmt.Domain.Messages;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Persistence.Repositories;

public class MessageReadRepository : IMessageReadRepository
{
    private readonly DbContext _db;
    public MessageReadRepository(DbContext db) => _db = db;

    public Task<int> CountUnreadAsync(Guid? userId, CancellationToken ct)
    {
        // Start from recipients; unread is per-recipient
        var recipients = _db.Set<MessageRecipient>().AsQueryable();

        if (userId is Guid uid)
            recipients = recipients.Where(r => r.UserId == uid);

        // Only count unread for messages that were actually sent
        var q =
            from r in recipients.Where(r => !r.IsRead)
            join m in _db.Set<Message>() on r.MessageId equals m.Id
            where m.Status == MessageStatus.Sent
            select r.Id;

        return q.CountAsync(ct);
    }

    public async Task<(int total, IReadOnlyList<UnreadMessageItemDto> items)> GetUnreadPagedAsync(Guid? userId, int page, int pageSize, CancellationToken ct)
    {
        var recipients = _db.Set<MessageRecipient>().AsQueryable();

        if (userId is Guid uid)
            recipients = recipients.Where(r => r.UserId == uid);

        var baseQuery =
            from r in recipients.Where(r => !r.IsRead)
            join m in _db.Set<Message>() on r.MessageId equals m.Id
            where m.Status == MessageStatus.Sent
            select new { r, m };

        var total = 0;


        return (total, items: null);
    }
}
