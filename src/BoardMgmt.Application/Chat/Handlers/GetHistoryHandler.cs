namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Domain.Entities;

public sealed class GetHistoryHandler : IRequestHandler<GetHistoryQuery, PagedResult<ChatMessageDto>>
{
    private readonly DbContext _db;
    public GetHistoryHandler(DbContext db) => _db = db;

    public async Task<PagedResult<ChatMessageDto>> Handle(GetHistoryQuery req, CancellationToken ct)
    {
        var member = await _db.Set<ConversationMember>()
            .FirstOrDefaultAsync(m => m.ConversationId == req.ConversationId && m.UserId == req.ForUserId, ct);
        if (member is null) throw new UnauthorizedAccessException();

        var q = _db.Set<ChatMessage>()
            .Where(m => m.ConversationId == req.ConversationId && m.DeletedAtUtc == null);

        if (req.ThreadRootId.HasValue)
            q = q.Where(m => m.ThreadRootId == req.ThreadRootId.Value);
        else
            q = q.Where(m => m.ThreadRootId == null);

        if (req.BeforeUtc.HasValue)
            q = q.Where(m => m.CreatedAtUtc < req.BeforeUtc.Value);

        var take = req.Take <= 0 ? 50 : req.Take;

        var items = await q.OrderByDescending(m => m.CreatedAtUtc)
            .Take(take)
            .Select(m => new
            {
                m.Id,
                m.ConversationId,
                m.ThreadRootId,
                m.SenderId,
                m.BodyHtml,
                m.CreatedAtUtc,
                m.EditedAtUtc,
                m.DeletedAtUtc,
                Attachments = m.Attachments.Select(a => new ChatAttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSize)).ToList(),
                Reactions = m.Reactions.GroupBy(r => r.Emoji)
                                       .Select(g => new { Emoji = g.Key, Count = g.Count() })
                                       .ToList(),
                ThreadReplyCount = _db.Set<ChatMessage>().Count(x => x.ThreadRootId == m.Id)
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var senderIds = items.Select(i => i.SenderId).Distinct().ToList();
        var users = await _db.Set<AppUser>()
            .Where(u => senderIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct);

        static string? NameOf(string? first, string? last)
            => string.IsNullOrWhiteSpace(last) ? first : $"{first} {last}";

        // For reactedByMe, ChatReaction.UserId is Guid: check if current user id parses to Guid and compare.
        Guid? meGuid = Guid.TryParse(req.ForUserId, out var g) ? g : (Guid?)null;

        var dtos = items.Select(i =>
        {
            var u = users.FirstOrDefault(x => x.Id == i.SenderId);
            var from = new MinimalUserDto(i.SenderId, NameOf(u?.FirstName, u?.LastName), u?.Email);

            //var reacts = i.Reactions
            //    .Select(r => new ReactionDto(r.Emoji, r.Count,
            //        ReactedByMe: meGuid.HasValue &&
            //                     _db.Set<ChatReaction>().Any(x => x.MessageId == i.Id && x.Emoji == r.Emoji && x.UserId == meGuid.Value)))
            //    .ToList();

            var reacts = i.Reactions
                .Select(r => new ReactionDto(
                    r.Emoji,
                    r.Count,
                    ReactedByMe: _db.Set<ChatReaction>()
                        .Any(x => x.MessageId == i.Id && x.Emoji == r.Emoji && x.UserId == req.ForUserId)
    ))
    .ToList();

            return new ChatMessageDto(i.Id, i.ConversationId, i.ThreadRootId, from, i.BodyHtml,
                i.CreatedAtUtc, i.EditedAtUtc, i.DeletedAtUtc != null, i.Attachments, reacts, i.ThreadReplyCount);
        }).ToList();

        return new PagedResult<ChatMessageDto>(dtos, dtos.Count);
    }
}
