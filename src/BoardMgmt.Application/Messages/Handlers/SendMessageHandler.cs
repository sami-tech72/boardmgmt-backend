using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Messages.Commands;
using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Application.Messages._Mapping;
using BoardMgmt.Domain.Messages;

public class SendMessageHandler : IRequestHandler<SendMessageCommand, MessageDto>
{
    private readonly DbContext _db;
    public SendMessageHandler(DbContext db) => _db = db;

    public async Task<MessageDto> Handle(SendMessageCommand req, CancellationToken ct)
    {
        var msg = await _db.Set<Message>()
            .Include(m => m.Recipients)
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == req.MessageId, ct)
            ?? throw new KeyNotFoundException("Message not found");

        if (!msg.Recipients.Any())
            throw new InvalidOperationException("Message must have at least one recipient");

        msg.Status = MessageStatus.Sent;
        msg.SentAtUtc = DateTime.UtcNow;
        msg.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MessageMapping.ToDto(msg);
    }
}
