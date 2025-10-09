using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Identity;
using BoardMgmt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Auth
{
    public class PermissionService : IPermissionService, IRolePermissionStore
    {
        private readonly AppDbContext _db;
        private readonly RoleManager<IdentityRole> _roles;
        private readonly ICurrentUser _current;

        public PermissionService(AppDbContext db, RoleManager<IdentityRole> roles, ICurrentUser current)
        {
            _db = db;
            _roles = roles;
            _current = current;
        }

        public async Task<Permission> GetMineAsync(AppModule module, CancellationToken ct)
        {
            var roleNames = _current.Roles;
            if (roleNames.Count == 0) return Permission.None;

            var roleIds = await _roles.Roles
                .Where(r => roleNames.Contains(r.Name!))
                .Select(r => r.Id)
                .ToListAsync(ct);

            var allowedList = await _db.RolePermissions
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

        public async Task<IDictionary<int, int>> GetAggregatedForRoleAsync(string roleId, CancellationToken ct)
        {
            var rows = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => new { Module = (int)rp.Module, Allowed = (int)rp.Allowed })
                .ToListAsync(ct);

            var result = new Dictionary<int, int>();

            foreach (var row in rows)
            {
                if (result.TryGetValue(row.Module, out var existing))
                {
                    result[row.Module] = existing | row.Allowed;
                }
                else
                {
                    result[row.Module] = row.Allowed;
                }
            }

            return result;
        }

        public async Task<IDictionary<string, IDictionary<int, int>>> GetAggregatedForRolesAsync(
            IEnumerable<string> roleIds, CancellationToken ct)
        {
            var idSet = roleIds.ToHashSet();
            if (idSet.Count == 0)
            {
                return new Dictionary<string, IDictionary<int, int>>();
            }

            var rows = await _db.RolePermissions
                .Where(rp => idSet.Contains(rp.RoleId))
                .Select(rp => new { rp.RoleId, Module = (int)rp.Module, Allowed = (int)rp.Allowed })
                .ToListAsync(ct);

            var result = new Dictionary<string, IDictionary<int, int>>();

            foreach (var row in rows)
            {
                if (!result.TryGetValue(row.RoleId, out var moduleMap))
                {
                    moduleMap = new Dictionary<int, int>();
                    result[row.RoleId] = moduleMap;
                }

                if (moduleMap.TryGetValue(row.Module, out var existing))
                {
                    moduleMap[row.Module] = existing | row.Allowed;
                }
                else
                {
                    moduleMap[row.Module] = row.Allowed;
                }
            }

            return result;
        }
    }
}
