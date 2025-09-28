using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Application.Documents.Queries.ListDocuments;
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

        // ⛔️ Nothing selected -> show no documents at top-level
        if (string.IsNullOrWhiteSpace(request.FolderSlug))
            return Array.Empty<DocumentDto>();

        var q = db.Documents.AsNoTracking()
            .Where(d => d.RoleAccesses.Any(ra => myRoleIds.Contains(ra.RoleId)));

        // ---- Folder filter (normalize to "root"; compare lower) ----
        var want = request.FolderSlug.Trim().ToLower();
        if (want == "root")
        {
            q = q.Where(d =>
                d.FolderSlug == null ||
                d.FolderSlug == "" ||
                d.FolderSlug.ToLower() == "root"
            );
        }
        else
        {
            q = q.Where(d =>
                d.FolderSlug != null &&
                d.FolderSlug != "" &&
                d.FolderSlug.ToLower() == want
            );
        }

        // ---- Type filter (case-insensitive) ----
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var t = request.Type.Trim().ToLower();
            q = t switch
            {
                "pdf" => q.Where(d => d.ContentType.ToLower().Contains("pdf")),
                "powerpoint" => q.Where(d => d.ContentType.ToLower().Contains("presentation")
                                          || d.ContentType.ToLower().Contains("powerpoint")
                                          || d.ContentType.ToLower().Contains("ppt")),
                "excel" => q.Where(d => d.ContentType.ToLower().Contains("spreadsheet")
                                          || d.ContentType.ToLower().Contains("excel")
                                          || d.ContentType.ToLower().Contains("sheet")),
                "word" => q.Where(d => d.ContentType.ToLower().Contains("word")
                                          || d.ContentType.ToLower().Contains("msword")
                                          || d.ContentType.ToLower().Contains("officedocument.wordprocessingml")),
                _ => q
            };
        }

        // ---- Search (case-insensitive; LIKE for better indexes) ----
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            q = q.Where(d =>
                EF.Functions.Like(d.OriginalName.ToLower(), $"%{s}%") ||
                (d.Description != null && EF.Functions.Like(d.Description.ToLower(), $"%{s}%"))
            );
        }

        // ---- Date preset ----
        if (!string.IsNullOrWhiteSpace(request.DatePreset))
        {
            var now = DateTimeOffset.UtcNow;
            switch (request.DatePreset.Trim().ToLower())
            {
                case "today":
                    var startUtc = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
                    q = q.Where(d => d.UploadedAt >= startUtc);
                    break;
                case "week":
                    q = q.Where(d => d.UploadedAt >= now.AddDays(-7));
                    break;
                case "month":
                    q = q.Where(d => d.UploadedAt >= now.AddDays(-30));
                    break;
            }
        }

        return await q
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(
                d.Id, d.OriginalName, d.Url, d.ContentType, d.SizeBytes,
                d.Version, d.FolderSlug, d.MeetingId, d.Description, d.UploadedAt
            ))
            .ToListAsync(ct);
    }
}
