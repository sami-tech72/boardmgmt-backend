using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Documents.Commands.ReplaceDocumentFile;

public class ReplaceDocumentFileCommandHandler(
    IAppDbContext db,
    IFileStorage storage,
    IPermissionService perms
) : IRequestHandler<ReplaceDocumentFileCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(ReplaceDocumentFileCommand request, CancellationToken ct)
    {
        await perms.EnsureMineAsync(AppModule.Documents, Permission.Update, ct);

        var doc = await db.Documents.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (doc is null) throw new KeyNotFoundException("Document not found.");

        var requestedName = string.IsNullOrWhiteSpace(request.OriginalName)
            ? doc.OriginalName
            : request.OriginalName.Trim();

        var newExt = Path.GetExtension(requestedName);
        if (string.IsNullOrEmpty(newExt))
        {
            var oldExt = Path.GetExtension(doc.OriginalName);
            if (!string.IsNullOrEmpty(oldExt))
                requestedName += oldExt;
        }

        var (savedName, url) = await storage.SaveAsync(request.Content, requestedName, request.ContentType, ct);

        doc.OriginalName = Path.GetFileName(requestedName);
        doc.FileName = savedName;
        doc.Url = url;
        doc.ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? doc.ContentType : request.ContentType;
        doc.SizeBytes = request.SizeBytes;
        doc.Version += 1;

        await db.SaveChangesAsync(ct);

        return new DocumentDto(
            doc.Id, doc.OriginalName, doc.Url, doc.ContentType, doc.SizeBytes,
            doc.Version, doc.FolderSlug, doc.MeetingId, doc.Description, doc.UploadedAt
        );
    }
}
