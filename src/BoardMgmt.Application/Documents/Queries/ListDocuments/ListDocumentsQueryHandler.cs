using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Documents.Queries.ListDocuments;

public sealed class ListDocumentsQueryHandler
    : IRequestHandler<ListDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    private static readonly Dictionary<string, string[]> TypeMap = new()
    {
        ["pdf"] = new[] { "application/pdf" },
        ["word"] = new[] { "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        ["excel"] = new[] { "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        ["powerpoint"] = new[] { "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
    };

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _user;

    public ListDocumentsQueryHandler(IAppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<IReadOnlyList<DocumentDto>> Handle(ListDocumentsQuery request, CancellationToken ct)
    {
        var q = _db.Documents.AsQueryable();

        // Default to root folder if not specified
        if (string.IsNullOrWhiteSpace(request.FolderSlug))
            q = q.Where(d => d.FolderSlug == "root");
        else
            q = q.Where(d => d.FolderSlug == request.FolderSlug);

        // Filter by type
        if (!string.IsNullOrWhiteSpace(request.Type) && TypeMap.TryGetValue(request.Type!, out var mimes))
            q = q.Where(d => mimes.Contains(d.ContentType));

        // Search text
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            q = q.Where(d =>
                d.OriginalName.Contains(s) ||
                (d.Description != null && d.Description.Contains(s)));
        }

        // Date preset
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

        // Role → Access mask (Secretary maps to Admins)
        var mask = DocumentAccess.None;
        foreach (var r in _user.Roles)
        {
            switch (r)
            {
                case "Admin": mask |= DocumentAccess.Administrators; break;
                case "Secretary": mask |= DocumentAccess.Administrators; break;
                case "BoardMember": mask |= DocumentAccess.BoardMembers; break;
                case "CommitteeMember": mask |= DocumentAccess.CommitteeMembers; break;
                case "Observer": mask |= DocumentAccess.Observers; break;
            }
        }

        q = (mask == DocumentAccess.None)
            ? q.Where(d => (d.Access & DocumentAccess.Observers) != 0)
            : q.Where(d => (d.Access & mask) != 0);

        return await q
            .OrderByDescending(d => d.UploadedAt)
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
                d.UploadedAt,
                d.Access))
            .ToListAsync(ct);
    }
}
