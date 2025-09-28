// DownloadDocumentQuery.cs
using MediatR;

namespace BoardMgmt.Application.Documents.Queries.DownloadDocument;

public sealed record DownloadFileResult(Stream Stream, string ContentType, string DownloadFileName);
public sealed record DownloadDocumentQuery(Guid DocumentId) : IRequest<DownloadFileResult>;
