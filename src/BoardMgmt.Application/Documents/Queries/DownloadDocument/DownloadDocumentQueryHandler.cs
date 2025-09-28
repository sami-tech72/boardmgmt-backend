// DownloadDocumentQueryHandler.cs
using BoardMgmt.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Documents.Queries.DownloadDocument;

public sealed class DownloadDocumentQueryHandler(
    IAppDbContext db,
    IFileStorage storage
) : IRequestHandler<DownloadDocumentQuery, DownloadFileResult>
{
    public async Task<DownloadFileResult> Handle(DownloadDocumentQuery request, CancellationToken ct)
    {
        var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DocumentId, ct);
        if (doc is null) throw new KeyNotFoundException("Document not found.");

        var stream = await storage.OpenAsync(doc.Url, ct);
        var contentType = string.IsNullOrWhiteSpace(doc.ContentType) ? "application/octet-stream" : doc.ContentType;
        var downloadName = string.IsNullOrWhiteSpace(doc.OriginalName) ? "download.bin" : doc.OriginalName;

        return new DownloadFileResult(stream, contentType, downloadName);
    }
}
