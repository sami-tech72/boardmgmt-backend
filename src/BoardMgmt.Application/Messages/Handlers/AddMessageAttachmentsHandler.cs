using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Messages.Commands;
using BoardMgmt.Domain.Messages;

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
                MessageId = req.MessageId,
                FileName = f.FileName,
                ContentType = f.ContentType,
                FileSize = f.FileSize,
                StoragePath = f.StoragePath
            });
        }

        return await _db.SaveChangesAsync(ct);
    }
}
