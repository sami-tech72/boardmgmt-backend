//using BoardMgmt.Application.Documents.DTOs;
//using MediatR;
//using static BoardMgmt.Application.Documents.Commands.UploadDocuments.UploadDocumentsCommand;


//namespace BoardMgmt.Application.Documents.Commands.UploadDocuments;


//public sealed record UploadDocumentsCommand(
//Guid? MeetingId,
//string FolderSlug,
//string? Description,
//IReadOnlyList<UploadItem> Files
//) : IRequest<IReadOnlyList<DocumentDto>>
//{
//    public sealed record UploadItem(Stream Content, string OriginalName, string ContentType, long SizeBytes);
//}




using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using static BoardMgmt.Application.Documents.Commands.UploadDocuments.UploadDocumentsCommand;

namespace BoardMgmt.Application.Documents.Commands.UploadDocuments;

public sealed record UploadDocumentsCommand(
    Guid? MeetingId,
    string FolderSlug,
    string? Description,
    DocumentAccess Access,
    IReadOnlyList<UploadItem> Files
) : IRequest<IReadOnlyList<DocumentDto>>
{
    public sealed record UploadItem(Stream Content, string OriginalName, string ContentType, long SizeBytes);
}
