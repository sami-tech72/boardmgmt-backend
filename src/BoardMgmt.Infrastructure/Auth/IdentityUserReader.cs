using BoardMgmt.Application.Common.Identity;
using BoardMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Auth;

public sealed class IdentityUserReader : IIdentityUserReader
{
    private readonly AppDbContext _db;
    public IdentityUserReader(AppDbContext db) => _db = db;

    public async Task<List<MinimalIdentityUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var idList = ids?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList() ?? new();
        if (idList.Count == 0) return new();

        // AppDbContext : IdentityDbContext<AppUser> → _db.Users available
        return await _db.Users
            .Where(u => idList.Contains(u.Id))
            .Select(u => new MinimalIdentityUser(
                u.Id,
                string.IsNullOrWhiteSpace(u.UserName) ? u.Email : u.UserName, // no FullName needed
                u.Email
            ))
            .ToListAsync(ct);
    }
}
