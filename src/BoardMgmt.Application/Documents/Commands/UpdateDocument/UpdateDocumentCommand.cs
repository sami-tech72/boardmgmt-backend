using BoardMgmt.Application.Documents.DTOs;
using MediatR;

namespace BoardMgmt.Application.Documents.Commands.UpdateDocument;

public sealed record UpdateDocumentCommand(
    Guid Id,
    string? OriginalName,
    string? Description,
    string? FolderSlug,
    IReadOnlyList<string>? RoleIds
) : IRequest<DocumentDto>;
