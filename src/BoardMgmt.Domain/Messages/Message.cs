using System.Collections.Generic;

namespace BoardMgmt.Domain.Messages;

public class Message
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public MessagePriority Priority { get; set; } = MessagePriority.Normal;
    public bool ReadReceiptRequested { get; set; }
    public bool IsConfidential { get; set; }

    public MessageStatus Status { get; set; } = MessageStatus.Draft;
    public DateTime? SentAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<MessageRecipient> Recipients { get; set; } = new List<MessageRecipient>();
    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}
