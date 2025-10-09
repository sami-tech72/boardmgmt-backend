namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;
using BoardMgmt.Application.Common.Interfaces;

public sealed class LeaveConversationHandler : IRequestHandler<LeaveConversationCommand, bool>
{
    private readonly IAppDbContext _db;
    public LeaveConversationHandler(IAppDbContext db) => _db = db;

    public async Task<bool> Handle(LeaveConversationCommand req, CancellationToken ct)
    {
        var member = await _db.Set<ConversationMember>()
            .FirstOrDefaultAsync(m => m.ConversationId == req.ConversationId && m.UserId == req.UserId, ct);
        if (member is null) return false;

        _db.Remove(member);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
