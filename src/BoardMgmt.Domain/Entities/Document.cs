namespace BoardMgmt.Domain.Entities;


public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}