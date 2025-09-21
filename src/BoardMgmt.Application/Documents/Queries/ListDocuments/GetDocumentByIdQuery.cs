using System;
using BoardMgmt.Application.Documents.DTOs;
using MediatR;

namespace BoardMgmt.Application.Documents.Queries.GetDocumentById;

public sealed record GetDocumentByIdQuery(Guid Id) : IRequest<DocumentDto?>;
