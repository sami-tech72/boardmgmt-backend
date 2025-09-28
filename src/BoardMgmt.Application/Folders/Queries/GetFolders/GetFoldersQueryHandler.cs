// File: src/BoardMgmt.Application/Folders/Queries/GetFolders/GetFoldersQueryHandler.cs
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Folders.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Folders.Queries.GetFolders
{
    public sealed class GetFoldersQueryHandler : IRequestHandler<GetFoldersQuery, IReadOnlyList<FolderDto>>
    {
        private readonly IAppDbContext _db;
        private readonly IIdentityUserReader _users;

        public GetFoldersQueryHandler(IAppDbContext db, IIdentityUserReader users)
        {
            _db = db;
            _users = users;
        }

        public async Task<IReadOnlyList<FolderDto>> Handle(GetFoldersQuery request, CancellationToken ct)
        {
            // 1) Current user's Role IDs (must be IDs to match DocumentRoleAccess.RoleId)
            var myRoleIds = await _users.GetCurrentUserRoleIdsAsync(ct);

            // 2) Materialize counts per folderSlug (EF part)
            var countsList = (myRoleIds.Count == 0)
                ? new List<(string Slug, int Count)>()
                : await _db.Documents
                    .AsNoTracking()
                    .Where(d => d.RoleAccesses.Any(ra => myRoleIds.Contains(ra.RoleId)))
                    .GroupBy(d => d.FolderSlug)
                    .Select(g => new { Slug = g.Key!, Count = g.Count() })
                    .ToListAsync(ct)
                    .ContinueWith(t => t.Result.Select(x => (x.Slug, x.Count)).ToList(), ct);

            // 3) Build dictionary in memory (safe for TryGetValue)
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (slug, count) in countsList)
            {
                var key = string.IsNullOrWhiteSpace(slug) ? "root" : slug;
                counts[key] = count;
            }

            // 4) Materialize folders first (EF part)
            var folderEntities = await _db.Folders
                .AsNoTracking()
                .OrderBy(f => f.Id)
                .ToListAsync(ct);

            // 5) Map to DTOs in memory (TryGetValue is now fine)
            var folders = folderEntities
                .Select(f =>
                {
                    counts.TryGetValue(f.Slug, out var c);
                    return new FolderDto(f.Id, f.Name, f.Slug, c);
                })
                .ToList();



            return folders;
        }
    }
}
