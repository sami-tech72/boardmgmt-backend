using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Application.Messages.Queries;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using BoardMgmt.Domain.Messages;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Messages.Handlers;

public sealed class GetMessageViewHandler : IRequestHandler<GetMessageViewQuery, MessageDetailVm?>
{
    private readonly DbContext _db;
    public GetMessageViewHandler(DbContext db) => _db = db;

    public async Task<MessageDetailVm?> Handle(GetMessageViewQuery req, CancellationToken ct)
    {
        var m = await _db.Set<Message>()
            .Include(x => x.Recipients)
            .Include(x => x.Attachments)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);

        if (m == null) return null;

        // Collect all user ids we need (sender + recipients) as strings (AppUser.Id is string)
        var senderIdStr = m.SenderId.ToString();
        var recipIdStrs = m.Recipients
            .Select(r => r.UserId.ToString())
            .Distinct()
            .ToList();

        var allUserIds = recipIdStrs
            .Append(senderIdStr)
            .Distinct()
            .ToList();

        // Materialize users first; no Guid parsing inside EF queries
        var rawUsers = await _db.Set<AppUser>()
            .AsNoTracking()
            .Where(u => allUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct);

        // Map to MinimalUserDto in memory (safe Guid parsing)
        MinimalUserDto? MapUser(string? idStr)
        {
            if (string.IsNullOrWhiteSpace(idStr)) return null;
            var u = rawUsers.FirstOrDefault(x => x.Id == idStr);
            if (u is null) return null;

            _ = Guid.TryParse(u.Id, out var gid);
            var name = string.IsNullOrWhiteSpace(u.LastName) ? u.FirstName : $"{u.FirstName} {u.LastName}";
            return new MinimalUserDto(gid, name, u.Email);
        }

        var sender = MapUser(senderIdStr);

        var recipients = recipIdStrs
            .Select(MapUser)
            .Where(x => x is not null)
            .Cast<MinimalUserDto>()
            .ToList();

        var attachments = m.Attachments
            .Select(a => new MessageAttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSize))
            .ToList();

        return new MessageDetailVm(
            m.Id,
            m.Subject,
            m.Body,
            sender,
            recipients,
            m.Priority.ToString(),
            m.Status.ToString(),
            m.CreatedAtUtc,
            m.SentAtUtc,
            m.UpdatedAtUtc,
            attachments
        );
    }
}
