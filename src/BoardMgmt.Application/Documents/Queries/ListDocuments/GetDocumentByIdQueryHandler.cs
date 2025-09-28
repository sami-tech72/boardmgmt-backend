using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Application.Documents.Queries.GetDocumentById;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed class GetDocumentByIdQueryHandler(
    IAppDbContext db,
    IIdentityUserReader users
) : IRequestHandler<GetDocumentByIdQuery, DocumentDto?>
{
    public async Task<DocumentDto?> Handle(GetDocumentByIdQuery request, CancellationToken ct)
    {
        var myRoleIds = await users.GetCurrentUserRoleIdsAsync(ct);

        return await db.Documents
            .AsNoTracking()
            .Where(d => d.Id == request.Id)
            .Where(d => !d.RoleAccesses.Any() || d.RoleAccesses.Any(ra => myRoleIds.Contains(ra.RoleId)))
            .Select(d => new DocumentDto(
                d.Id, d.OriginalName, d.Url, d.ContentType, d.SizeBytes,
                d.Version, d.FolderSlug, d.MeetingId, d.Description, d.UploadedAt
            ))
            .FirstOrDefaultAsync(ct);
    }
}
