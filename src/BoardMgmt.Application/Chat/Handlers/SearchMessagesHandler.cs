namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using BoardMgmt.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Domain.Entities;

public sealed class SearchMessagesHandler : IRequestHandler<SearchMessagesQuery, IReadOnlyList<ChatMessageDto>>
{
    private readonly IAppDbContext _db;
    public SearchMessagesHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ChatMessageDto>> Handle(SearchMessagesQuery req, CancellationToken ct)
    {
        var myConvIds = await _db.Set<ConversationMember>()
            .Where(m => m.UserId == req.ForUserId)
            .Select(m => m.ConversationId)
            .ToListAsync(ct);

        if (myConvIds.Count == 0) return Array.Empty<ChatMessageDto>();

        var term = (req.Term ?? "").Trim();
        if (term.Length < 2) return Array.Empty<ChatMessageDto>();

        var q = _db.Set<ChatMessage>()
            .Where(m => myConvIds.Contains(m.ConversationId) && m.DeletedAtUtc == null &&
                        (EF.Functions.Like(m.BodyHtml, $"%{term}%")));

        var items = await q.OrderByDescending(m => m.CreatedAtUtc)
            .Take(req.Take <= 0 ? 50 : req.Take)
            .Select(m => new {
                m.Id,
                m.ConversationId,
                m.ThreadRootId,
                m.SenderId,
                m.BodyHtml,
                m.CreatedAtUtc,
                m.EditedAtUtc,
                Attachments = m.Attachments.Select(a => new ChatAttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSize)).ToList(),
                Reactions = m.Reactions.GroupBy(r => r.Emoji).Select(g => new { Emoji = g.Key, Count = g.Count() }).ToList(),
                ThreadReplyCount = _db.Set<ChatMessage>().Count(x => x.ThreadRootId == m.Id)
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var userIds = items.Select(i => i.SenderId).Distinct().ToList();
        var rawUsers = await _db.Set<AppUser>()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct);

        static string? NameOf(string? first, string? last)
            => string.IsNullOrWhiteSpace(last) ? first : $"{first} {last}";

        Guid? meGuid = Guid.TryParse(req.ForUserId, out var g) ? g : (Guid?)null;

        return items.Select(i =>
        {
            var u = rawUsers.FirstOrDefault(x => x.Id == i.SenderId);
            var from = new MinimalUserDto(i.SenderId, NameOf(u?.FirstName, u?.LastName), u?.Email);
            //var reacts = i.Reactions.Select(r => new ReactionDto(r.Emoji, r.Count,
            //    ReactedByMe: meGuid.HasValue && _db.Set<ChatReaction>().Any(x => x.MessageId == i.Id && x.Emoji == r.Emoji && x.UserId == meGuid.Value)
            //)).ToList();
            var reacts = i.Reactions.Select(r => new ReactionDto(
                r.Emoji,
                r.Count,
                ReactedByMe: _db.Set<ChatReaction>()
        .Any(x => x.MessageId == i.Id && x.Emoji == r.Emoji && x.UserId == req.ForUserId)
)).ToList();

            return new ChatMessageDto(i.Id, i.ConversationId, i.ThreadRootId, from, i.BodyHtml,
                i.CreatedAtUtc, i.EditedAtUtc, false, i.Attachments, reacts, i.ThreadReplyCount);
        }).ToList();
    }
}
