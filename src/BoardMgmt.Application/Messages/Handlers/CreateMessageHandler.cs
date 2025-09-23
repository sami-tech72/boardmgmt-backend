using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Application.Messages.Commands;
using BoardMgmt.Application.Messages.DTOs;
using BoardMgmt.Application.Messages._Mapping;
using BoardMgmt.Domain.Messages;

namespace BoardMgmt.Application.Messages.Handlers;

public class CreateMessageHandler : IRequestHandler<CreateMessageCommand, MessageDto>
{
    private readonly DbContext _db;
    public CreateMessageHandler(DbContext db) => _db = db;

    public async Task<MessageDto> Handle(CreateMessageCommand req, CancellationToken ct)
    {
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = req.SenderId,
            Subject = req.Subject.Trim(),
            Body = req.Body,
            Priority = Enum.Parse<MessagePriority>(req.Priority, ignoreCase: true),
            ReadReceiptRequested = req.ReadReceiptRequested,
            IsConfidential = req.IsConfidential,
            Status = req.AsDraft ? MessageStatus.Draft : MessageStatus.Sent,
            SentAtUtc = req.AsDraft ? null : DateTime.UtcNow,
        };

        foreach (var uid in req.RecipientIds.Distinct())
            msg.Recipients.Add(new MessageRecipient { Id = Guid.NewGuid(), MessageId = msg.Id, UserId = uid });

        _db.Set<Message>().Add(msg);
        await _db.SaveChangesAsync(ct);

        return MessageMapping.ToDto(msg);
    }
}
