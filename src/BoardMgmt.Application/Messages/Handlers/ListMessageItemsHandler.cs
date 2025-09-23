using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Application.Messages.Queries;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using BoardMgmt.Domain.Messages;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Messages.Handlers;

public sealed class ListMessageItemsHandler
    : IRequestHandler<ListMessageItemsQuery, PagedResult<MessageListItemVm>>
{
    private readonly DbContext _db;
    public ListMessageItemsHandler(DbContext db) => _db = db;

    public async Task<PagedResult<MessageListItemVm>> Handle(ListMessageItemsQuery req, CancellationToken ct)
    {
        var q = _db.Set<Message>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Status) &&
            Enum.TryParse<MessageStatus>(req.Status, true, out var st))
        {
            q = q.Where(m => m.Status == st);
        }

        if (!string.IsNullOrWhiteSpace(req.Priority) &&
            Enum.TryParse<MessagePriority>(req.Priority, true, out var pr))
        {
            q = q.Where(m => m.Priority == pr);
        }

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

        // Project only what we need; compute HasAttachments via .Any()
        var items = await q
            .OrderByDescending(m => m.SentAtUtc ?? m.UpdatedAtUtc)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .AsNoTracking()
            .Select(m => new
            {
                m.Id,
                m.Subject,
                m.Body,
                m.SenderId,
                m.Priority,
                m.Status,
                m.CreatedAtUtc,
                m.SentAtUtc,
                m.UpdatedAtUtc,
                HasAttachments = m.Attachments.Any()
            })
            .ToListAsync(ct);

        // Gather sender ids (AppUser.Id is string)
        var senderIdStrs = items
            .Select(i => i.SenderId.ToString())
            .Distinct()
            .ToList();

        // Materialize users first; NO TryParse inside EF!
        var rawUsers = await _db.Set<AppUser>()
            .AsNoTracking()
            .Where(u => senderIdStrs.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct);

        MinimalUserDto? MapUser(string idStr)
        {
            var u = rawUsers.FirstOrDefault(x => x.Id == idStr);
            if (u is null) return null;

            _ = Guid.TryParse(u.Id, out var gid);
            var name = string.IsNullOrWhiteSpace(u.LastName) ? u.FirstName : $"{u.FirstName} {u.LastName}";
            return new MinimalUserDto(gid, name, u.Email);
        }

        static string Preview(string body)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            var trimmed = body.Replace("\r", " ").Replace("\n", " ");
            return trimmed.Length > 140 ? trimmed[..140] + "…" : trimmed;
        }

        var vms = items.Select(i =>
            new MessageListItemVm(
                i.Id,
                i.Subject,
                Preview(i.Body),
                MapUser(i.SenderId.ToString()),
                i.Priority.ToString(),
                i.Status.ToString(),
                i.CreatedAtUtc,
                i.SentAtUtc,
                i.UpdatedAtUtc,
                i.HasAttachments
            )
        ).ToList();

        return new PagedResult<MessageListItemVm>(vms, total);
    }
}
