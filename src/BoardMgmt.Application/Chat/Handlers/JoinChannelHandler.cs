namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using BoardMgmt.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;

public sealed class JoinChannelHandler : IRequestHandler<JoinChannelCommand, bool>
{
    private readonly IAppDbContext _db;
    public JoinChannelHandler(IAppDbContext db) => _db = db;

    public async Task<bool> Handle(JoinChannelCommand req, CancellationToken ct)
    {
        var conv = await _db.Set<Conversation>().FirstOrDefaultAsync(c => c.Id == req.ConversationId, ct);
        if (conv is null) return false;
        if (conv.IsPrivate && conv.Type == ConversationType.Channel) return false;

        var exists = await _db.Set<ConversationMember>()
            .AnyAsync(m => m.ConversationId == req.ConversationId && m.UserId == req.UserId, ct);
        if (exists) return true;

        _db.Set<ConversationMember>().Add(new ConversationMember
        {
            Id = Guid.NewGuid(),
            ConversationId = req.ConversationId,
            UserId = req.UserId,
            Role = ConversationMemberRole.Member,
            JoinedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
