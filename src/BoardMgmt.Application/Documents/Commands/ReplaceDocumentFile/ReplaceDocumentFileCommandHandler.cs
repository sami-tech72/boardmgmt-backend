// Application/Documents/Commands/ReplaceDocumentFile/ReplaceDocumentFileCommandHandler.cs
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

        // Decide the user-facing display name:
        // - use the one sent in the command if provided
        // - otherwise keep existing doc.OriginalName
        var displayName = string.IsNullOrWhiteSpace(request.OriginalName)
            ? doc.OriginalName
            : request.OriginalName.Trim();

        // Save new binary (you can use displayName for the stored file name if desired)
        var (savedName, url) = await storage.SaveAsync(request.Content, displayName, request.ContentType, ct);

        // Persist fields
        doc.OriginalName = displayName;     // <-- Respect the display name from the request
        doc.FileName = savedName;       // physical/storage name
        doc.Url = url;
        doc.ContentType = request.ContentType;
        doc.SizeBytes = request.SizeBytes;
        doc.Version += 1;               // bump version for new binary

        await db.SaveChangesAsync(ct);

        return new DocumentDto(
            doc.Id, doc.OriginalName, doc.Url, doc.ContentType, doc.SizeBytes,
            doc.Version, doc.FolderSlug, doc.MeetingId, doc.Description, doc.UploadedAt
        );
    }
}
