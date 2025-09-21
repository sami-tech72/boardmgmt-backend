using BoardMgmt.Application.Documents.DTOs;
using MediatR;

namespace BoardMgmt.Application.Documents.Queries.ListDocuments;

public sealed record ListDocumentsQuery(
    string? FolderSlug,
    string? Type,
    string? Search,
    string? DatePreset
) : IRequest<IReadOnlyList<DocumentDto>>;
