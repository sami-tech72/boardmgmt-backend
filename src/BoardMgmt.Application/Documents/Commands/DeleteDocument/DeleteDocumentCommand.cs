using MediatR;

namespace BoardMgmt.Application.Documents.Commands.DeleteDocument;

public sealed record DeleteDocumentCommand(Guid Id) : IRequest<Unit>;
