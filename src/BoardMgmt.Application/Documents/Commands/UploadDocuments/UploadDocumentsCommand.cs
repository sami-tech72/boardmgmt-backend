using BoardMgmt.Application.Documents.DTOs;
using MediatR;
using static BoardMgmt.Application.Documents.Commands.UploadDocuments.UploadDocumentsCommand;

namespace BoardMgmt.Application.Documents.Commands.UploadDocuments;

public sealed record UploadDocumentsCommand(
    Guid? MeetingId,
    string FolderSlug,
    string? Description,
    IReadOnlyList<UploadItem> Files
) : IRequest<IReadOnlyList<DocumentDto>>
{
    public sealed record UploadItem(Stream Content, string OriginalName, string ContentType, long SizeBytes);
}
