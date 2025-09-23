using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Messages.Queries;
using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Application.Messages._Mapping;
using BoardMgmt.Domain.Messages;

public class ListMessagesHandler : IRequestHandler<ListMessagesQuery, PagedResult<MessageDto>>
{
    private readonly DbContext _db;
    public ListMessagesHandler(DbContext db) => _db = db;

    public async Task<PagedResult<MessageDto>> Handle(ListMessagesQuery req, CancellationToken ct)
    {
        var q = _db.Set<Message>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Status) && Enum.TryParse<MessageStatus>(req.Status, true, out var st))
            q = q.Where(m => m.Status == st);

        if (!string.IsNullOrWhiteSpace(req.Priority) && Enum.TryParse<MessagePriority>(req.Priority, true, out var pr))
            q = q.Where(m => m.Priority == pr);

        if (!string.IsNullOrWhiteSpace(req.Q))
        {
            var term = req.Q.Trim();
            q = q.Where(m =>
                            EF.Functions.Like(m.Subject, $"%{term}%") ||
                            EF.Functions.Like(m.Body, $"%{term}%"));

        }

        if (req.ForUserId.HasValue)
            q = q.Where(m => m.Recipients.Any(r => r.UserId == req.ForUserId.Value));

        if (req.SentByUserId.HasValue)
            q = q.Where(m => m.SenderId == req.SentByUserId.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(m => m.SentAtUtc ?? m.UpdatedAtUtc)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Include(m => m.Recipients).Include(m => m.Attachments)
            .ToListAsync(ct);

        return new PagedResult<MessageDto>(items.Select(MessageMapping.ToDto).ToList(), total);
    }
}
