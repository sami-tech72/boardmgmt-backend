namespace BoardMgmt.Application.Chat.Handlers;

using BoardMgmt.Application.Chat;
using BoardMgmt.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Chat;

public sealed class AddChatAttachmentsHandler : IRequestHandler<AddChatAttachmentsCommand, int>
{
    private readonly IAppDbContext _db;
    public AddChatAttachmentsHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(AddChatAttachmentsCommand req, CancellationToken ct)
    {
        var msg = await _db.Set<ChatMessage>().FirstOrDefaultAsync(m => m.Id == req.MessageId, ct)
                  ?? throw new KeyNotFoundException("Message not found");

        foreach (var f in req.Files)
        {
            _db.Set<ChatAttachment>().Add(new ChatAttachment
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
