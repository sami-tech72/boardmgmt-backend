namespace BoardMgmt.Application.Folders.DTOs;

public sealed record FolderDto(
    Guid Id,
    string Name,
    string Slug,
    int DocumentCount);
