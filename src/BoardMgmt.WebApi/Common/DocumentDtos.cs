namespace BoardMgmt.WebApi.Common;

public record FolderDto(Guid Id, string Name, string Slug, int DocumentCount);

public record DocumentDto(
    Guid Id,
    string Name,
    string Type,          // "pdf" | "word" | "excel" | "powerpoint" | "file"
    long SizeBytes,
    DateTimeOffset UploadedAt,
    string FolderSlug,
    Guid? MeetingId,
    string Url
);

public record CreateFolderRequest(string Name);

public record ListDocumentsQuery(string? Folder = null, string? Type = null, string? Search = null);
