using System;
using System.Collections.Generic;
using BoardMgmt.Domain.Common;

namespace BoardMgmt.Domain.Entities;

public class Document : AuditableEntity
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

    public ICollection<DocumentRoleAccess> RoleAccesses { get; set; } = new List<DocumentRoleAccess>();
}
