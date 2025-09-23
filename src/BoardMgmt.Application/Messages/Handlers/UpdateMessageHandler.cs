using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Messages.Commands;
using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Application.Messages._Mapping;
using BoardMgmt.Domain.Messages;

public class UpdateMessageHandler : IRequestHandler<UpdateMessageCommand, MessageDto>
{
    private readonly DbContext _db;
    public UpdateMessageHandler(DbContext db) => _db = db;

    public async Task<MessageDto> Handle(UpdateMessageCommand req, CancellationToken ct)
    {
        var msg = await _db.Set<Message>()
            .Include(m => m.Recipients)
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == req.MessageId, ct)
            ?? throw new KeyNotFoundException("Message not found");

        if (msg.Status == MessageStatus.Sent) throw new InvalidOperationException("Cannot edit a sent message");

        msg.Subject = req.Subject.Trim();
        msg.Body = req.Body;
        msg.Priority = Enum.Parse<MessagePriority>(req.Priority, ignoreCase: true);
        msg.ReadReceiptRequested = req.ReadReceiptRequested;
        msg.IsConfidential = req.IsConfidential;
        msg.UpdatedAtUtc = DateTime.UtcNow;

        // sync recipients
        var newSet = req.RecipientIds.Distinct().ToHashSet();
        msg.Recipients = msg.Recipients.Where(r => newSet.Contains(r.UserId)).ToList();
        var existing = msg.Recipients.Select(r => r.UserId).ToHashSet();
        foreach (var add in newSet.Except(existing))
            msg.Recipients.Add(new MessageRecipient { Id = Guid.NewGuid(), MessageId = msg.Id, UserId = add });

        await _db.SaveChangesAsync(ct);
        return MessageMapping.ToDto(msg);
    }
}
