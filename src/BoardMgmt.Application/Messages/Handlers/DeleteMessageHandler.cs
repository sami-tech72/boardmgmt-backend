using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Messages.Commands;
using BoardMgmt.Domain.Messages;

public class DeleteMessageHandler : IRequestHandler<DeleteMessageCommand, bool>
{
    private readonly DbContext _db;
    public DeleteMessageHandler(DbContext db) => _db = db;

    public async Task<bool> Handle(DeleteMessageCommand req, CancellationToken ct)
    {
        var msg = await _db.Set<Message>().FirstOrDefaultAsync(m => m.Id == req.MessageId, ct);
        if (msg is null) return false;
        _db.Set<Message>().Remove(msg);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
