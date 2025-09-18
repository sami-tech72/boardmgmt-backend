using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using MediatR;

namespace BoardMgmt.Application.Documents.Commands.UploadDocuments;

public class UploadDocumentsCommandHandler(
    IAppDbContext db,
    IFileStorage storage,
    IPermissionService perms) : IRequestHandler<UploadDocumentsCommand, IReadOnlyList<DocumentDto>>
{
    public async Task<IReadOnlyList<DocumentDto>> Handle(UploadDocumentsCommand request, CancellationToken ct)
    {
        // Permission: need Create on Documents
        await perms.EnsureMineAsync(AppModule.Documents, Permission.Create, ct);

        if (string.IsNullOrWhiteSpace(request.FolderSlug))
            request = request with { FolderSlug = "root" };

        var results = new List<DocumentDto>();

        foreach (var f in request.Files)
        {
            var (savedName, url) = await storage.SaveAsync(f.Content, f.OriginalName, f.ContentType, ct);

            var doc = new Document
            {
                MeetingId = request.MeetingId,
                FolderSlug = request.FolderSlug,
                FileName = savedName,
                OriginalName = f.OriginalName,
                Url = url,
                ContentType = f.ContentType,
                SizeBytes = f.SizeBytes,
                Description = request.Description,
                UploadedAt = DateTimeOffset.UtcNow
            };

            db.Documents.Add(doc);

            results.Add(new DocumentDto(
                doc.Id, doc.OriginalName, doc.Url, doc.ContentType, doc.SizeBytes,
                doc.Version, doc.FolderSlug, doc.MeetingId, doc.Description, doc.UploadedAt));
        }

        await db.SaveChangesAsync(ct);
        return results;
    }
}
