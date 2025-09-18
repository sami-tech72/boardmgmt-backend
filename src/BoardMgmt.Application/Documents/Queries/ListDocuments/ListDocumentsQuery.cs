using BoardMgmt.Application.Documents.DTOs;
using MediatR;

namespace BoardMgmt.Application.Documents.Queries.ListDocuments;

public sealed record ListDocumentsQuery(
    string? FolderSlug,
    string? Type,      // "pdf"|"word"|"excel"|"powerpoint" (optional)
    string? Search,    // text in OriginalName/Description
    string? DatePreset // "today" | "week" | "month" | null
) : IRequest<IReadOnlyList<DocumentDto>>;
