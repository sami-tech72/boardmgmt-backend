using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Application.Documents.Queries.ListDocuments;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

public class ListDocumentsQueryHandler(
    IAppDbContext db,
    IIdentityUserReader users
) : IRequestHandler<ListDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    public async Task<IReadOnlyList<DocumentDto>> Handle(ListDocumentsQuery request, CancellationToken ct)
    {
        var myRoleIds = await users.GetCurrentUserRoleIdsAsync(ct);
        if (myRoleIds.Count == 0)
            return Array.Empty<DocumentDto>();

        // documents visible if any DocumentRoleAccess.RoleId ∈ myRoleIds
        var q = db.Documents.AsNoTracking()
            .Where(d => d.RoleAccesses.Any(ra => myRoleIds.Contains(ra.RoleId)));

        // filters
        if (!string.IsNullOrWhiteSpace(request.FolderSlug))
            q = q.Where(d => d.FolderSlug == request.FolderSlug);

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var t = request.Type.ToLower();
            q = t switch
            {
                "pdf" => q.Where(d => d.ContentType.Contains("pdf")),
                "powerpoint" => q.Where(d => d.ContentType.Contains("presentation") || d.ContentType.Contains("powerpoint") || d.ContentType.Contains("ppt")),
                "excel" => q.Where(d => d.ContentType.Contains("spreadsheet") || d.ContentType.Contains("excel") || d.ContentType.Contains("sheet")),
                "word" => q.Where(d => d.ContentType.Contains("word") || d.ContentType.Contains("msword") || d.ContentType.Contains("officedocument.wordprocessingml")),
                _ => q
            };
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            q = q.Where(d => d.OriginalName.Contains(s) || (d.Description != null && d.Description.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(request.DatePreset))
        {
            var now = DateTimeOffset.UtcNow;
            q = request.DatePreset.ToLower() switch
            {
                "today" => q.Where(d => d.UploadedAt >= now.Date),
                "week" => q.Where(d => d.UploadedAt >= now.AddDays(-7)),
                "month" => q.Where(d => d.UploadedAt >= now.AddDays(-30)),
                _ => q
            };
        }

        return await q.OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(
                d.Id, d.OriginalName, d.Url, d.ContentType, d.SizeBytes,
                d.Version, d.FolderSlug, d.MeetingId, d.Description, d.UploadedAt
            ))
            .ToListAsync(ct);
    }
}
