//namespace BoardMgmt.Domain.Entities;

//public class Document
//{
//    public Guid Id { get; set; } = Guid.NewGuid();

//    // Nullable: can upload general docs not tied to a meeting
//    public Guid? MeetingId { get; set; }

//    // Link to Folder via Slug (simple taxonomy)
//    public string FolderSlug { get; set; } = "root";

//    public string FileName { get; set; } = string.Empty; // stored file name
//    public string OriginalName { get; set; } = string.Empty; // user file name
//    public string Url { get; set; } = string.Empty; // /uploads/... path (web)
//    public string ContentType { get; set; } = "application/octet-stream";
//    public long SizeBytes { get; set; }

//    public int Version { get; set; } = 1;
//    public string? Description { get; set; }
//    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;












//}


namespace BoardMgmt.Domain.Entities;

[Flags]
public enum DocumentAccess : int
{
    None = 0,
    BoardMembers = 1 << 0,
    CommitteeMembers = 1 << 1,
    Observers = 1 << 2,
    Administrators = 1 << 3,
    All = BoardMembers | CommitteeMembers | Observers | Administrators
}

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? MeetingId { get; set; }
    public string FolderSlug { get; set; } = "root";

    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }

    public int Version { get; set; } = 1;
    public string? Description { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    // NEW: who can access this document
    public DocumentAccess Access { get; set; } =
        DocumentAccess.Administrators | DocumentAccess.BoardMembers;
}
