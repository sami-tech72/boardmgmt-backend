using BoardMgmt.Application.Documents.DTOs;
using MediatR;

namespace BoardMgmt.Application.Documents.Commands.ReplaceDocumentFile;

public sealed record ReplaceDocumentFileCommand(
    Guid Id,
    string OriginalName,
    string ContentType,
    long SizeBytes,
    Stream Content
) : IRequest<DocumentDto>;
