// Application/Documents/Commands/UpdateDocument/UpdateDocumentCommandHandler.cs
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Documents.Commands.UpdateDocument;

public class UpdateDocumentCommandHandler(
    IAppDbContext db,
    IPermissionService perms
) : IRequestHandler<UpdateDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UpdateDocumentCommand request, CancellationToken ct)
    {
        await perms.EnsureMineAsync(AppModule.Documents, Permission.Update, ct);

        var doc = await db.Documents
            .Include(d => d.RoleAccesses)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);

        if (doc is null) throw new KeyNotFoundException("Document not found.");

        if (!string.IsNullOrWhiteSpace(request.OriginalName))
            doc.OriginalName = request.OriginalName.Trim();

        if (request.Description is not null)
            doc.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (!string.IsNullOrWhiteSpace(request.FolderSlug))
            doc.FolderSlug = request.FolderSlug.Trim();

        // RoleIds: null -> do not change; empty -> clear; values -> overwrite
        if (request.RoleIds is not null)
        {
            doc.RoleAccesses.Clear();
            foreach (var rid in request.RoleIds.Distinct())
                doc.RoleAccesses.Add(new DocumentRoleAccess { RoleId = rid, DocumentId = doc.Id });
        }

        await db.SaveChangesAsync(ct);

        return new DocumentDto(
            doc.Id, doc.OriginalName, doc.Url, doc.ContentType, doc.SizeBytes,
            doc.Version, doc.FolderSlug, doc.MeetingId, doc.Description, doc.UploadedAt
        );
    }
}
