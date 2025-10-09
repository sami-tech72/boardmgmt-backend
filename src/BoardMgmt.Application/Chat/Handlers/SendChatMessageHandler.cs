namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Application.Common.Interfaces;

public sealed class SendChatMessageHandler : IRequestHandler<SendChatMessageCommand, Guid>
{
    private readonly IAppDbContext _db;
    public SendChatMessageHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(SendChatMessageCommand req, CancellationToken ct)
    {
        var member = await _db.Set<ConversationMember>()
            .AnyAsync(m => m.ConversationId == req.ConversationId && m.UserId == req.SenderId, ct);
        if (!member) throw new UnauthorizedAccessException();

        var msg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = req.ConversationId,
            SenderId = req.SenderId,
            ThreadRootId = req.ThreadRootId,
            BodyHtml = req.BodyHtml?.Trim() ?? "",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Set<ChatMessage>().Add(msg);

        var conv = await _db.Set<Conversation>().FirstAsync(c => c.Id == req.ConversationId, ct);
        conv.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return msg.Id;
    }
}
