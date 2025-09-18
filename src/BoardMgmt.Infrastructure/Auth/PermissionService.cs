using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Identity;
using BoardMgmt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Auth
{
    public class PermissionService(
        AppDbContext db,
        RoleManager<IdentityRole> roles,
        ICurrentUser current) : IPermissionService
    {
        public async Task<Permission> GetMineAsync(AppModule module, CancellationToken ct)
        {
            var roleNames = current.Roles;
            if (roleNames.Count == 0) return Permission.None;

            var roleIds = await roles.Roles
                .Where(r => roleNames.Contains(r.Name!))
                .Select(r => r.Id)
                .ToListAsync(ct);

            var allowedList = await db.RolePermissions
                .Where(p => roleIds.Contains(p.RoleId) && p.Module == module)
                .Select(p => p.Allowed)
                .ToListAsync(ct);

            var mask = Permission.None;
            foreach (var a in allowedList) mask |= a;
            return mask;
        }

        public async Task<bool> HasMineAsync(AppModule module, Permission needed, CancellationToken ct)
            => (await GetMineAsync(module, ct) & needed) == needed;

        public async Task EnsureMineAsync(AppModule module, Permission needed, CancellationToken ct)
        {
            if (!await HasMineAsync(module, needed, ct))
                throw new UnauthorizedAccessException("Insufficient permissions.");
        }
    }
}
