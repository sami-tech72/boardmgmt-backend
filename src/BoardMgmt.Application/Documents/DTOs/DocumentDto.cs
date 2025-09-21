namespace BoardMgmt.Application.Documents.DTOs;

public sealed record DocumentDto(
    Guid Id,
    string OriginalName,
    string Url,
    string ContentType,
    long SizeBytes,
    int Version,
    string FolderSlug,
    Guid? MeetingId,
    string? Description,
    DateTimeOffset UploadedAt


);
