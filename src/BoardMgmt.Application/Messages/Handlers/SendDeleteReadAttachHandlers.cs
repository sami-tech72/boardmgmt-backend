using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Messages;
using BoardMgmt.Application.Messages.Commands;

public class SendMessageHandler : IRequestHandler<SendMessageCommand, bool>
{
    private readonly DbContext _db;
    public SendMessageHandler(DbContext db) => _db = db;

    public async Task<bool> Handle(SendMessageCommand req, CancellationToken ct)
    {
        var msg = await _db.Set<Message>().Include(m => m.Recipients)
            .FirstOrDefaultAsync(m => m.Id == req.MessageId, ct) ?? throw new KeyNotFoundException();
        if (!msg.Recipients.Any()) throw new InvalidOperationException("Message must have at least one recipient");
        msg.Status = MessageStatus.Sent;
        msg.SentAtUtc = DateTime.UtcNow;
        msg.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public class DeleteMessageHandler : IRequestHandler<DeleteMessageCommand, bool>
{
    private readonly DbContext _db;
    public DeleteMessageHandler(DbContext db) => _db = db;

    public async Task<bool> Handle(DeleteMessageCommand req, CancellationToken ct)
    {
        var msg = await _db.Set<Message>().FirstOrDefaultAsync(m => m.Id == req.MessageId, ct);
        if (msg is null) return false;
        _db.Remove(msg);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

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

public class AddMessageAttachmentsHandler : IRequestHandler<AddMessageAttachmentsCommand, int>
{
    private readonly DbContext _db;
    public AddMessageAttachmentsHandler(DbContext db) => _db = db;

    public async Task<int> Handle(AddMessageAttachmentsCommand req, CancellationToken ct)
    {
        var msg = await _db.Set<Message>().FirstOrDefaultAsync(m => m.Id == req.MessageId, ct)
                  ?? throw new KeyNotFoundException("Message not found");
        foreach (var f in req.Files)
        {
            _db.Set<MessageAttachment>().Add(new MessageAttachment
            {
                Id = Guid.NewGuid(),
                MessageId = msg.Id,
                FileName = f.FileName,
                ContentType = f.ContentType,
                FileSize = f.FileSize,
                StoragePath = f.StoragePath
            });
        }
        return await _db.SaveChangesAsync(ct);
    }
}
