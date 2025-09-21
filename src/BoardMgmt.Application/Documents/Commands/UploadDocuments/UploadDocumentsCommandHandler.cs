using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Documents.Commands.UploadDocuments;

public class UploadDocumentsCommandHandler(
    IAppDbContext db,
    IFileStorage storage,
    IPermissionService perms,
    IIdentityUserReader users // to infer default RoleIds if none are sent
) : IRequestHandler<UploadDocumentsCommand, IReadOnlyList<DocumentDto>>
{
    public async Task<IReadOnlyList<DocumentDto>> Handle(UploadDocumentsCommand request, CancellationToken ct)
    {
        await perms.EnsureMineAsync(AppModule.Documents, Permission.Create, ct);

        var folder = string.IsNullOrWhiteSpace(request.FolderSlug) ? "root" : request.FolderSlug.Trim();

        // If client didn't send RoleIds, default to the uploader's current roles
        var roleIds = request.RoleIds is { Count: > 0 }
            ? request.RoleIds.Distinct().ToList()
            : (await users.GetCurrentUserRoleIdsAsync(ct)).ToList(); // implement GetCurrentUserRoleIdsAsync

        if (roleIds.Count == 0)
            throw new InvalidOperationException("No roles provided and current user has no roles.");

        var results = new List<DocumentDto>();

        foreach (var f in request.Files)
        {
            var (savedName, url) = await storage.SaveAsync(f.Content, f.OriginalName, f.ContentType, ct);

            var doc = new Document
            {
                MeetingId = request.MeetingId,
                FolderSlug = folder,
                FileName = savedName,
                OriginalName = f.OriginalName,
                Url = url,
                ContentType = f.ContentType,
                SizeBytes = f.SizeBytes,
                Description = request.Description,
                UploadedAt = DateTimeOffset.UtcNow
            };

            // attach role access rows
            foreach (var rid in roleIds)
                doc.RoleAccesses.Add(new DocumentRoleAccess { RoleId = rid, Document = doc });

            db.Documents.Add(doc);

            results.Add(new DocumentDto(
                doc.Id, doc.OriginalName, doc.Url, doc.ContentType, doc.SizeBytes,
                doc.Version, doc.FolderSlug, doc.MeetingId, doc.Description, doc.UploadedAt
            ));
        }

        await db.SaveChangesAsync(ct);
        return results;
    }
}
