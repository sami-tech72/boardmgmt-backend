using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Documents.Queries.GetDocumentById;

public sealed class GetDocumentByIdQueryHandler : IRequestHandler<GetDocumentByIdQuery, DocumentDto?>
{
    private readonly DbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetDocumentByIdQueryHandler(DbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DocumentDto?> Handle(GetDocumentByIdQuery request, CancellationToken ct)
    {
        // Use RoleIds property from your ICurrentUser (already in your interface)
         var myRoleIds = await _currentUser.GetRoleIdsAsync(ct);
        //var myRoleIds = _currentUser.RoleIds ?? [];

        return await _db.Set<Document>()
            .AsNoTracking()
            .Where(d => d.Id == request.Id)
            // Access rule:
            // - No restrictions => visible to everyone
            // - Otherwise, user must have ANY of the required roleIds
            .Where(d => !d.RoleAccesses.Any() ||
                        d.RoleAccesses.Any(ra => myRoleIds.Contains(ra.RoleId)))
            .Select(d => new DocumentDto(
                d.Id,
                d.OriginalName,
                d.Url,
                d.ContentType,
                d.SizeBytes,
                d.Version,
                d.FolderSlug,
                d.MeetingId,
                d.Description,
                d.UploadedAt
            ))
            .FirstOrDefaultAsync(ct);
    }
}
