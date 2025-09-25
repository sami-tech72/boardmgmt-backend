namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;

public sealed class EditChatMessageHandler : IRequestHandler<EditChatMessageCommand, bool>
{
    private readonly DbContext _db;
    public EditChatMessageHandler(DbContext db) => _db = db;

    public async Task<bool> Handle(EditChatMessageCommand req, CancellationToken ct)
    {
        var msg = await _db.Set<ChatMessage>().FirstOrDefaultAsync(m => m.Id == req.MessageId, ct);
        if (msg is null) return false;
        if (!string.Equals(msg.SenderId, req.EditorId, StringComparison.Ordinal)) throw new UnauthorizedAccessException();
        if (msg.DeletedAtUtc != null) return false;

        msg.BodyHtml = req.BodyHtml?.Trim() ?? "";
        msg.EditedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
