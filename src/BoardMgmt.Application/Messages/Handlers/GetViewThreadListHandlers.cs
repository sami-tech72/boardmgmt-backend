using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Application.Messages.Queries;
using BoardMgmt.Application.Messages._Mapping;
using BoardMgmt.Domain.Messages;

public class GetMessageViewHandler : IRequestHandler<GetMessageViewQuery, MessageDetailVm?>
{
    private readonly DbContext _db;
    public GetMessageViewHandler(DbContext db) => _db = db;

    public async Task<MessageDetailVm?> Handle(GetMessageViewQuery req, CancellationToken ct)
    {
        var m = await _db.Set<Message>()
            .Include(x => x.Recipients).Include(x => x.Attachments)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (m is null) return null;

        var ids = m.Recipients.Select(r => r.UserId.ToString()).Append(m.SenderId.ToString()).Distinct().ToList();
        var users = await _db.Set<AppUser>()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .AsNoTracking().ToListAsync(ct);

        BoardMgmt.Application.Messages.DTOs.MinimalUserDto? Map(string id)
        {
            var u = users.FirstOrDefault(x => x.Id == id);
            if (u is null) return null;
            _ = Guid.TryParse(u.Id, out var gid);
            var name = string.IsNullOrWhiteSpace(u.LastName) ? u.FirstName : $"{u.FirstName} {u.LastName}";
            return new(gid, name, u.Email);
        }

        var sender = Map(m.SenderId.ToString());
        var recips = m.Recipients.Select(r => Map(r.UserId.ToString()))!.Where(x => x != null)!.Cast<BoardMgmt.Application.Messages.DTOs.MinimalUserDto>().ToList();
        var atts = m.Attachments.Select(a => new BoardMgmt.Application.Messages.DTOs.MessageAttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSize)).ToList();

        return new MessageDetailVm(m.Id, m.Subject, m.Body, sender, recips, m.Priority.ToString(), m.Status.ToString(), m.CreatedAtUtc, m.SentAtUtc, m.UpdatedAtUtc, atts);
    }
}

public class GetMessageThreadHandler : IRequestHandler<GetMessageThreadQuery, MessageThreadVm>
{
    private readonly DbContext _db;
    public GetMessageThreadHandler(DbContext db) => _db = db;

    public async Task<MessageThreadVm> Handle(GetMessageThreadQuery req, CancellationToken ct)
    {
        var anchor = await _db.Set<Message>().Include(m => m.Recipients).Include(m => m.Attachments)
            .AsNoTracking().FirstOrDefaultAsync(m => m.Id == req.AnchorMessageId, ct) ?? throw new KeyNotFoundException();

        string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var x = s.Trim();
            var re = new Regex(@"^(re|fwd)\s*:\s*", RegexOptions.IgnoreCase);
            while (re.IsMatch(x)) x = re.Replace(x, "").Trim();
            return x.ToLowerInvariant();
        }

        var key = Normalize(anchor.Subject);
        var current = req.CurrentUserId;
        var otherIds = new HashSet<Guid>(anchor.Recipients.Select(r => r.UserId));
        if (anchor.SenderId != current) otherIds.Add(anchor.SenderId);
        otherIds.Remove(current);

        var candidates = await _db.Set<Message>()
            .Include(m => m.Recipients).Include(m => m.Attachments)
            .Where(m => (m.SenderId == current && m.Recipients.Any(r => otherIds.Contains(r.UserId))) ||
                        (otherIds.Contains(m.SenderId) && m.Recipients.Any(r => r.UserId == current)))
            .AsNoTracking().ToListAsync(ct);

        var thread = candidates.Where(m => Normalize(m.Subject) == key).OrderBy(m => m.CreatedAtUtc).ToList();

        var userIds = thread.Select(m => m.SenderId.ToString())
            .Concat(thread.SelectMany(m => m.Recipients.Select(r => r.UserId.ToString())))
            .Append(current.ToString()).Distinct().ToList();

        var rawUsers = await _db.Set<AppUser>().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email }).AsNoTracking().ToListAsync(ct);

        BoardMgmt.Application.Messages.DTOs.MinimalUserDto Map(Guid id)
        {
            var s = id.ToString();
            var u = rawUsers.FirstOrDefault(x => x.Id == s);
            var name = (u is null) ? null : (string.IsNullOrWhiteSpace(u.LastName) ? u.FirstName : $"{u.FirstName} {u.LastName}");
            return new(id, name, u?.Email);
        }

        var bubbles = thread.Select(m => new BoardMgmt.Application.Messages.DTOs.MessageBubbleVm(
            m.Id, Map(m.SenderId), m.Body, m.CreatedAtUtc,
            m.Attachments.Select(a => new BoardMgmt.Application.Messages.DTOs.MessageAttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSize)).ToList()
        )).ToList();

        var participants = new List<BoardMgmt.Application.Messages.DTOs.MinimalUserDto> { Map(current) };
        participants.AddRange(otherIds.Select(Map));

        return new MessageThreadVm(anchor.Id, anchor.Subject, participants, bubbles);
    }
}

public class ListMessageItemsHandler : IRequestHandler<ListMessageItemsQuery, PagedResult<MessageListItemVm>>
{
    private readonly DbContext _db;
    public ListMessageItemsHandler(DbContext db) => _db = db;

    public async Task<PagedResult<MessageListItemVm>> Handle(ListMessageItemsQuery req, CancellationToken ct)
    {
        var q = _db.Set<Message>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Status) && Enum.TryParse<MessageStatus>(req.Status, true, out var st)) q = q.Where(m => m.Status == st);
        if (!string.IsNullOrWhiteSpace(req.Priority) && Enum.TryParse<MessagePriority>(req.Priority, true, out var pr)) q = q.Where(m => m.Priority == pr);
        if (!string.IsNullOrWhiteSpace(req.Q))
        {
            var term = req.Q.Trim();
            q = q.Where(m => EF.Functions.Like(m.Subject, $"%{term}%") || EF.Functions.Like(m.Body, $"%{term}%"));
        }
        if (req.ForUserId.HasValue) q = q.Where(m => m.Recipients.Any(r => r.UserId == req.ForUserId.Value));
        if (req.SentByUserId.HasValue) q = q.Where(m => m.SenderId == req.SentByUserId.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(m => m.SentAtUtc ?? m.UpdatedAtUtc)
            .Skip((req.Page - 1) * req.PageSize).Take(req.PageSize).AsNoTracking()
            .Select(m => new { m.Id, m.Subject, m.Body, m.SenderId, m.Priority, m.Status, m.CreatedAtUtc, m.SentAtUtc, m.UpdatedAtUtc, HasAttachments = m.Attachments.Any() })
            .ToListAsync(ct);

        var senderStrIds = items.Select(i => i.SenderId.ToString()).Distinct().ToList();
        var rawUsers = await _db.Set<AppUser>().Where(u => senderStrIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email }).AsNoTracking().ToListAsync(ct);

        BoardMgmt.Application.Messages.DTOs.MinimalUserDto? Map(string idStr)
        {
            var u = rawUsers.FirstOrDefault(x => x.Id == idStr);
            if (u is null) return null;
            _ = Guid.TryParse(u.Id, out var gid);
            var name = string.IsNullOrWhiteSpace(u.LastName) ? u.FirstName : $"{u.FirstName} {u.LastName}";
            return new(gid, name, u.Email);
        }

        static string Preview(string body)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            var trimmed = body.Replace("\r", " ").Replace("\n", " ");
            return trimmed.Length > 140 ? trimmed[..140] + "…" : trimmed;
        }

        var vms = items.Select(i => new MessageListItemVm(
            i.Id, i.Subject, Preview(i.Body), Map(i.SenderId.ToString()),
            i.Priority.ToString(), i.Status.ToString(), i.CreatedAtUtc, i.SentAtUtc, i.UpdatedAtUtc, i.HasAttachments
        )).ToList();

        return new PagedResult<MessageListItemVm>(vms, total);
    }
}
