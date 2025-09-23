using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Messages.Commands;
using BoardMgmt.Domain.Messages;

namespace BoardMgmt.Application.Messages.Handlers;

public class MarkMessageReadHandler : IRequestHandler<MarkMessageReadCommand, bool>
{
    private readonly DbContext _db;
    public MarkMessageReadHandler(DbContext db) => _db = db;

    public async Task<bool> Handle(MarkMessageReadCommand req, CancellationToken ct)
    {
        var recip = await _db.Set<MessageRecipient>()
            .FirstOrDefaultAsync(r => r.MessageId == req.MessageId && r.UserId == req.UserId, ct);

        if (recip is null) return false;
        if (!recip.IsRead)
        {
            recip.IsRead = true;
            recip.ReadAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }
}
