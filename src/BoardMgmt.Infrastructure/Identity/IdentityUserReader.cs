using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;



namespace BoardMgmt.Infrastructure.Identity
{
    /// <summary>Identity-backed implementation to read users by ID list.</summary>
    public class IdentityUserReader(UserManager<AppUser> userManager) : IIdentityUserReader
    {
        public async Task<IReadOnlyList<AppUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct)
        {
            var idArray = ids?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray() ?? [];
            if (idArray.Length == 0) return Array.Empty<AppUser>();

            return await userManager.Users
                                    .Where(u => idArray.Contains(u.Id))
                                    .ToListAsync(ct);
        }
    }
}
