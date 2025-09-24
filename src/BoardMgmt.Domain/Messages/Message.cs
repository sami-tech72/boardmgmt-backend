namespace BoardMgmt.Domain.Messages;

public enum MessagePriority { Low = 0, Normal = 1, High = 2, Urgent = 3 }
public enum MessageStatus { Draft = 0, Sent = 1 }

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

public class MessageRecipient
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

public class MessageAttachment
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
}
