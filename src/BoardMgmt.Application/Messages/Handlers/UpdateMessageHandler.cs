using MediatR;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Messages;
using BoardMgmt.Application.Messages.Commands;

public class UpdateMessageHandler : IRequestHandler<UpdateMessageCommand, bool>
{
    private readonly DbContext _db;
    public UpdateMessageHandler(DbContext db) => _db = db;

    public async Task<bool> Handle(UpdateMessageCommand req, CancellationToken ct)
    {
        var msg = await _db.Set<Message>().Include(m => m.Recipients)
            .FirstOrDefaultAsync(m => m.Id == req.MessageId, ct) ?? throw new KeyNotFoundException();
        if (msg.Status == MessageStatus.Sent) throw new InvalidOperationException("Cannot edit a sent message");

        msg.Subject = (req.Subject ?? string.Empty).Trim();
        msg.Body = req.Body ?? string.Empty;
        msg.Priority = Enum.Parse<MessagePriority>(req.Priority, true);
        msg.ReadReceiptRequested = req.ReadReceiptRequested;
        msg.IsConfidential = req.IsConfidential;
        msg.UpdatedAtUtc = DateTime.UtcNow;

        var newSet = req.RecipientIds.Distinct().ToHashSet();
        msg.Recipients = msg.Recipients.Where(r => newSet.Contains(r.UserId)).ToList();
        var existing = msg.Recipients.Select(r => r.UserId).ToHashSet();
        foreach (var add in newSet.Except(existing))
            msg.Recipients.Add(new MessageRecipient { Id = Guid.NewGuid(), MessageId = msg.Id, UserId = add });

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
