namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Application.Common.Interfaces;

public sealed class ListConversationsHandler : IRequestHandler<ListConversationsQuery, IReadOnlyList<ConversationListItemDto>>
{
    private readonly IAppDbContext _db;
    public ListConversationsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ConversationListItemDto>> Handle(ListConversationsQuery req, CancellationToken ct)
    {
        var myMemberships = await _db.Set<ConversationMember>()
            .Where(m => m.UserId == req.ForUserId)
            .Select(m => new { m.ConversationId, m.LastReadAtUtc })
            .ToListAsync(ct);

        if (myMemberships.Count == 0) return Array.Empty<ConversationListItemDto>();
        var convIds = myMemberships.Select(x => x.ConversationId).Distinct().ToList();

        var meta = await _db.Set<Conversation>()
            .Where(c => convIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.Type,
                c.Name,
                c.IsPrivate,
                LastMessageAtUtc = _db.Set<ChatMessage>().Where(m => m.ConversationId == c.Id)
                    .Select(m => (DateTime?)m.CreatedAtUtc).OrderByDescending(x => x).FirstOrDefault()
            })
            .ToListAsync(ct);

        // unread per conversation
        var result = new List<ConversationListItemDto>(meta.Count);
        foreach (var m in meta)
        {
            var lastRead = myMemberships.First(x => x.ConversationId == m.Id).LastReadAtUtc;
            var unread = await _db.Set<ChatMessage>().CountAsync(
                x => x.ConversationId == m.Id
                     && (lastRead == null || x.CreatedAtUtc > lastRead)
                     && x.SenderId != req.ForUserId, ct);

            var name = m.Type == ConversationType.Channel ? (m.Name ?? "channel") : (m.Name ?? "direct");
            result.Add(new ConversationListItemDto(m.Id, name, m.Type.ToString(), m.IsPrivate, unread, m.LastMessageAtUtc));
        }

        return result.OrderByDescending(x => x.LastMessageAtUtc ?? DateTime.MinValue).ToList();
    }
}
