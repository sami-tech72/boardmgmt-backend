using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Identity;
using BoardMgmt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Auth
{
    public class PermissionService : IPermissionService, IRolePermissionStore
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;

        public PermissionService(AppDbContext db, ICurrentUser current)
        {
            _db = db;
            _current = current;
        }

        public async Task<Permission> GetMineAsync(AppModule module, CancellationToken ct)
        {
            var userId = _current.UserId;
            if (string.IsNullOrEmpty(userId)) return Permission.None;

            var roleIds = await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync(ct);

            if (roleIds.Count == 0) return Permission.None;

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

            return rows
                .GroupBy(r => r.Module)
                .ToDictionary(
                    g => g.Key,
                    g => g.Aggregate(0, (mask, row) => mask | row.Allowed));
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

            return rows
                .GroupBy(r => r.RoleId)
                .ToDictionary(
                    g => g.Key,
                    g => (IDictionary<int, int>)g
                        .GroupBy(row => row.Module)
                        .ToDictionary(
                            mg => mg.Key,
                            mg => mg.Aggregate(0, (mask, row) => mask | row.Allowed)));
        }
    }
}
