using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Identity;                 // 👈 AppModule, Permission
using BoardMgmt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Auth
{
    public class PermissionService(
        AppDbContext db,
        RoleManager<IdentityRole> roles,
        ICurrentUser current)
        : IPermissionService, IRolePermissionStore // 👈 implement both
    {
        // ===== Existing methods you already had =====
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

        // ===== NEW: IRolePermissionStore aggregation =====

        // Single role → { moduleId -> bitmask }
        public async Task<IDictionary<int, int>> GetAggregatedForRoleAsync(string roleId, CancellationToken ct)
        {
            var list = await db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .GroupBy(rp => (int)rp.Module)
                .Select(g => new
                {
                    Module = g.Key,
                    Allowed = g.Select(x => (int)x.Allowed).Aggregate(0, (a, b) => a | b)
                })
                .ToListAsync(ct);

            return list.ToDictionary(x => x.Module, x => x.Allowed);
        }

        // Many roles → { roleId -> { moduleId -> bitmask } }
        public async Task<IDictionary<string, IDictionary<int, int>>> GetAggregatedForRolesAsync(
            IEnumerable<string> roleIds, CancellationToken ct)
        {
            var idSet = roleIds.ToHashSet();
            var rows = await db.RolePermissions
                .Where(rp => idSet.Contains(rp.RoleId))
                .Select(rp => new { rp.RoleId, Module = (int)rp.Module, Allowed = (int)rp.Allowed })
                .ToListAsync(ct);

            return rows
                .GroupBy(r => r.RoleId)
                .ToDictionary(
                    g => g.Key,
                    g => (IDictionary<int, int>)g
                        .GroupBy(x => x.Module)
                        .ToDictionary(
                            mg => mg.Key,
                            mg => mg.Select(x => x.Allowed).Aggregate(0, (a, b) => a | b)
                        )
                );
        }
    }
}
