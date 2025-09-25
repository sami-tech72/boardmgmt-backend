namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;

public sealed class MarkConversationReadHandler : IRequestHandler<MarkConversationReadCommand, bool>
{
    private readonly DbContext _db;
    public MarkConversationReadHandler(DbContext db) => _db = db;

    public async Task<bool> Handle(MarkConversationReadCommand req, CancellationToken ct)
    {
        var member = await _db.Set<ConversationMember>()
            .FirstOrDefaultAsync(m => m.ConversationId == req.ConversationId && m.UserId == req.UserId, ct);
        if (member is null) return false;

        if (member.LastReadAtUtc == null || member.LastReadAtUtc < req.ReadAtUtc)
        {
            member.LastReadAtUtc = req.ReadAtUtc;
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }
}
