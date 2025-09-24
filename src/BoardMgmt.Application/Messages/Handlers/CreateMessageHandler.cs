using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Messages;
using BoardMgmt.Application.Messages.Commands;

public class CreateMessageHandler : IRequestHandler<CreateMessageCommand, Guid>
{
    private readonly DbContext _db;
    public CreateMessageHandler(DbContext db) => _db = db;

    public async Task<Guid> Handle(CreateMessageCommand req, CancellationToken ct)
    {
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            SenderId = req.SenderId,
            Subject = (req.Subject ?? string.Empty).Trim(),
            Body = req.Body ?? string.Empty,
            Priority = Enum.Parse<MessagePriority>(req.Priority, true),
            ReadReceiptRequested = req.ReadReceiptRequested,
            IsConfidential = req.IsConfidential,
            Status = req.AsDraft ? MessageStatus.Draft : MessageStatus.Sent,
            SentAtUtc = req.AsDraft ? null : DateTime.UtcNow
        };

        foreach (var uid in req.RecipientIds.Distinct())
            msg.Recipients.Add(new MessageRecipient { Id = Guid.NewGuid(), MessageId = msg.Id, UserId = uid });

        _db.Set<Message>().Add(msg);
        await _db.SaveChangesAsync(ct);
        return msg.Id;
    }
}
