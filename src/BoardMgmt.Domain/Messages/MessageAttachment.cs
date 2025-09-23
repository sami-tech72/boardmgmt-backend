namespace BoardMgmt.Domain.Messages;

public class MessageAttachment
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty; // disk or blob path
}
