using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Folders.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;


namespace BoardMgmt.Application.Folders.Queries.GetFolders;


public sealed class GetFoldersQueryHandler : IRequestHandler<GetFoldersQuery, IReadOnlyList<FolderDto>>
{
    private readonly IAppDbContext _db;
    public GetFoldersQueryHandler(IAppDbContext db) => _db = db;


    public async Task<IReadOnlyList<FolderDto>> Handle(GetFoldersQuery request, CancellationToken ct)
    {
        // Count documents per folderSlug efficiently
        var counts = await _db.Documents
        .GroupBy(d => d.FolderSlug)
        .Select(g => new { g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.Key!, x => x.Count, ct);


        var folders = await _db.Folders
        .OrderByDescending(f => f.CreatedAt)
        .Select(f => new FolderDto(f.Id, f.Name, f.Slug, counts.ContainsKey(f.Slug) ? counts[f.Slug] : 0))
        .ToListAsync(ct);


        // Also include implicit root
        var rootCount = counts.ContainsKey("root") ? counts["root"] : 0;
        folders.Add(new FolderDto(Guid.Empty, "Root", "root", rootCount));
        return folders;
    }
}