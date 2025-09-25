namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;

public sealed class DeleteChatMessageHandler : IRequestHandler<DeleteChatMessageCommand, bool>
{
    private readonly DbContext _db;
    public DeleteChatMessageHandler(DbContext db) => _db = db;

    public async Task<bool> Handle(DeleteChatMessageCommand req, CancellationToken ct)
    {
        var msg = await _db.Set<ChatMessage>().FirstOrDefaultAsync(m => m.Id == req.MessageId, ct);
        if (msg is null) return false;
        if (!string.Equals(msg.SenderId, req.RequestorId, StringComparison.Ordinal)) throw new UnauthorizedAccessException();
        if (msg.DeletedAtUtc != null) return true;

        msg.BodyHtml = "";
        msg.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
