using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Documents.Queries.ListDocuments;

public sealed class ListDocumentsQueryHandler(
    IAppDbContext db,
    IPermissionService perms) : IRequestHandler<ListDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    private static readonly Dictionary<string, string[]> TypeMap = new()
    {
        ["pdf"] = new[] { "application/pdf" },
        ["word"] = new[] { "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        ["excel"] = new[] { "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        ["powerpoint"] = new[] { "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
    };

    public async Task<IReadOnlyList<DocumentDto>> Handle(ListDocumentsQuery request, CancellationToken ct)
    {
        // Permission: need View (and Page) on Documents
        var canView = await perms.HasMineAsync(AppModule.Documents, Permission.View | Permission.Page, ct);
        if (!canView) throw new UnauthorizedAccessException("You cannot view documents.");

        var q = db.Documents.AsQueryable();

        // folder
        q = string.IsNullOrWhiteSpace(request.FolderSlug)
            ? q.Where(d => d.FolderSlug == "root")
            : q.Where(d => d.FolderSlug == request.FolderSlug);

        // type
        if (!string.IsNullOrWhiteSpace(request.Type) && TypeMap.TryGetValue(request.Type!, out var mimes))
            q = q.Where(d => mimes.Contains(d.ContentType));

        // search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            q = q.Where(d => d.OriginalName.Contains(s) || (d.Description != null && d.Description.Contains(s)));
        }

        // date preset
        if (!string.IsNullOrWhiteSpace(request.DatePreset))
        {
            var now = DateTimeOffset.UtcNow;
            DateTimeOffset start = request.DatePreset!.ToLower() switch
            {
                "today" => now.Date,
                "week" => now.AddDays(-7),
                "month" => now.AddMonths(-1),
                _ => DateTimeOffset.MinValue
            };
            if (start > DateTimeOffset.MinValue)
                q = q.Where(d => d.UploadedAt >= start);
        }

        return await q.OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(
                d.Id, d.OriginalName, d.Url, d.ContentType, d.SizeBytes,
                d.Version, d.FolderSlug, d.MeetingId, d.Description, d.UploadedAt))
            .ToListAsync(ct);
    }
}
