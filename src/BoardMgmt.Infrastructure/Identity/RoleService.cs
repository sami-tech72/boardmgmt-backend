using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Roles.Commands.SetRolePermissions;
using BoardMgmt.Domain.Identity;
using BoardMgmt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Identity
{
    public class RoleService : IRoleService
    {
        private readonly RoleManager<IdentityRole> _roles;
        private readonly AppDbContext _db;

        private const string PermissionClaimType = "permission";

        public RoleService(RoleManager<IdentityRole> roles, AppDbContext db)
        {
            _roles = roles;
            _db = db;
        }

        // Optional convenience (not required by IRoleService)
        public Task<bool> RoleExistsAsync(string name, CancellationToken ct)
            => _roles.RoleExistsAsync(name);

        public async Task<(bool ok, string? roleId, string[] errors)> CreateRoleAsync(string name, CancellationToken ct)
        {
            var existing = await _roles.FindByNameAsync(name);
            if (existing != null) return (true, existing.Id, Array.Empty<string>());

            var role = new IdentityRole(name);
            var res = await _roles.CreateAsync(role);
            return res.Succeeded
                ? (true, role.Id, Array.Empty<string>())
                : (false, null, res.Errors.Select(e => e.Description).ToArray());
        }

        public async Task<IReadOnlyList<(string Id, string Name)>> GetAllAsync(CancellationToken ct)
            => await _roles.Roles
                .Select(r => new ValueTuple<string, string>(r.Id, r.Name!))
                .ToListAsync(ct);

        public async Task<(bool ok, string[] errors)> RenameRoleAsync(string roleId, string name, CancellationToken ct)
        {
            var role = await _roles.FindByIdAsync(roleId);
            if (role is null) return (false, new[] { "Role not found." });

            role.Name = name;
            role.NormalizedName = name.ToUpperInvariant();
            var res = await _roles.UpdateAsync(role);
            return res.Succeeded
                ? (true, Array.Empty<string>())
                : (false, res.Errors.Select(e => e.Description).ToArray());
        }

        public async Task<(bool ok, string[] errors)> DeleteRoleAsync(string roleId, CancellationToken ct)
        {
            // Remove our custom table rows
            var olds = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
            _db.RolePermissions.RemoveRange(olds);

            // Remove role-level permission claims
            var oldClaims = await _db.Set<IdentityRoleClaim<string>>()
                .Where(rc => rc.RoleId == roleId && rc.ClaimType == PermissionClaimType)
                .ToListAsync(ct);
            _db.RemoveRange(oldClaims);

            await _db.SaveChangesAsync(ct);

            var role = await _roles.FindByIdAsync(roleId);
            if (role is null) return (true, Array.Empty<string>());

            var res = await _roles.DeleteAsync(role);
            return res.Succeeded
                ? (true, Array.Empty<string>())
                : (false, res.Errors.Select(e => e.Description).ToArray());
        }

        public async Task<IReadOnlyList<SavedRolePermission>> SetRolePermissionsAsync(
            string roleId,
            IEnumerable<(AppModule module, Permission allowed)> items,
            CancellationToken ct)
        {
            // 1) Replace rows in our RolePermissions table
            var olds = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
            _db.RolePermissions.RemoveRange(olds);

            var normalized = items
                .Select(i => (i.module, allowed: i.allowed.Normalize()))
                .Where(i => i.allowed != Permission.None)
                .ToList();

            var entities = normalized
                .Select(i => new RolePermission
                {
                    RoleId = roleId,
                    Module = i.module,
                    Allowed = i.allowed
                })
                .ToList();

            if (entities.Count > 0)
                await _db.RolePermissions.AddRangeAsync(entities, ct);

            // 2) Replace RoleClaims snapshot for this role
            var oldClaims = await _db.Set<IdentityRoleClaim<string>>()
                .Where(rc => rc.RoleId == roleId && rc.ClaimType == PermissionClaimType)
                .ToListAsync(ct);
            _db.RemoveRange(oldClaims);

            foreach (var e in entities)
            {
                _db.Add(new IdentityRoleClaim<string>
                {
                    RoleId = roleId,
                    ClaimType = PermissionClaimType,
                    ClaimValue = $"{(int)e.Module}:{(int)e.Allowed}"
                });
            }

            await _db.SaveChangesAsync(ct);

            // 3) Mirror to affected users’ UserClaims
            await UpdateUsersPermissionClaimsForRoleAsync(roleId, ct);

            return entities.Select(e => new SavedRolePermission(e.Id, e.Module, e.Allowed)).ToList();
        }

        public async Task<(bool ok, string[] errors)> UpdateRoleAndPermissionsAsync(
            string roleId,
            string name,
            IEnumerable<(AppModule module, Permission allowed)> items,
            CancellationToken ct)
        {
            // Use resilient execution strategy to allow retries + a user transaction
            var strategy = _db.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync<(bool ok, string[] errors)>(async () =>
                {
                    await using var tx = await _db.Database.BeginTransactionAsync(ct);
                    try
                    {
                        // Rename via RoleManager (must share SAME scoped AppDbContext)
                        var role = await _roles.FindByIdAsync(roleId);
                        if (role is null)
                        {
                            await tx.RollbackAsync(ct);
                            return (false, new[] { "Role not found." });
                        }

                        role.Name = name;
                        role.NormalizedName = name.ToUpperInvariant();

                        var renameRes = await _roles.UpdateAsync(role);
                        if (!renameRes.Succeeded)
                        {
                            await tx.RollbackAsync(ct);
                            return (false, renameRes.Errors.Select(e => e.Description).ToArray());
                        }

                        // Replace RolePermissions
                        var olds = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
                        _db.RolePermissions.RemoveRange(olds);

                        var normalized = items
                            .Select(i => (i.module, allowed: i.allowed.Normalize()))
                            .Where(i => i.allowed != Permission.None)
                            .ToList();

                        var ents = normalized.Select(i => new RolePermission
                        {
                            RoleId = roleId,
                            Module = i.module,
                            Allowed = i.allowed
                        }).ToList();

                        if (ents.Count > 0)
                            await _db.RolePermissions.AddRangeAsync(ents, ct);

                        // Replace RoleClaims snapshot
                        var oldClaims = await _db.Set<IdentityRoleClaim<string>>()
                            .Where(rc => rc.RoleId == roleId && rc.ClaimType == PermissionClaimType)
                            .ToListAsync(ct);
                        _db.RemoveRange(oldClaims);

                        foreach (var e in ents)
                        {
                            _db.Add(new IdentityRoleClaim<string>
                            {
                                RoleId = roleId,
                                ClaimType = PermissionClaimType,
                                ClaimValue = $"{(int)e.Module}:{(int)e.Allowed}"
                            });
                        }

                        await _db.SaveChangesAsync(ct);
                        await tx.CommitAsync(ct);

                        // Mirror to affected users (outside tx is fine; still under strategy scope)
                        await UpdateUsersPermissionClaimsForRoleAsync(roleId, ct);

                        return (true, Array.Empty<string>());
                    }
                    catch
                    {
                        await tx.RollbackAsync(ct);
                        throw; // allow strategy to retry
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, new[] { ex.Message });
            }
        }

        public async Task<IReadOnlyList<string>> GetAllRoleNamesAsync(CancellationToken ct) =>
            await _roles.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync(ct);

        public async Task<string?> GetRoleIdByNameAsync(string roleName, CancellationToken ct)
        {
            var role = await _roles.Roles
                .Where(r => r.Name == roleName)
                .Select(r => new { r.Id })
                .FirstOrDefaultAsync(ct);
            return role?.Id;
        }

        public async Task<IReadOnlyList<PermissionDto>> GetRolePermissionsAsync(string roleId, CancellationToken ct)
        {
            var rows = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => new { rp.Module, rp.Allowed })
                .ToListAsync(ct);

            var list = new List<PermissionDto>(rows.Count);
            foreach (var r in rows)
            {
                var normalizedInt = PermissionExtensions.NormalizeInt((int)r.Allowed);
                list.Add(new PermissionDto((AppModule)Convert.ToInt32(r.Module), (Permission)normalizedInt));
            }
            return list;
        }

        // --- helpers ---

        /// <summary>
        /// For each user in the given role, recompute union of ALL their roles’ permissions and
        /// replace their type="permission" user claims accordingly.
        /// </summary>
        private async Task UpdateUsersPermissionClaimsForRoleAsync(string roleId, CancellationToken ct)
        {
            // All affected users
            var userIds = await _db.UserRoles
                .Where(ur => ur.RoleId == roleId)
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(ct);

            if (userIds.Count == 0) return;

            // Preload all role memberships for these users
            var memberships = await _db.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .GroupBy(ur => ur.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.RoleId).ToArray(), ct);

            var allRoleIds = memberships.Values.SelectMany(v => v).Distinct().ToArray();

            var rolePermRows = await _db.RolePermissions
                .Where(rp => allRoleIds.Contains(rp.RoleId))
                .Select(rp => new { rp.RoleId, Module = (int)rp.Module, Allowed = (int)rp.Allowed })
                .ToListAsync(ct);

            var byRole = rolePermRows
                .GroupBy(r => r.RoleId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.Module)
                          .ToDictionary(mg => mg.Key, mg => mg.Select(x => x.Allowed).Aggregate(0, (a, b) => a | b))
                );

            // For each user, compute union mask per module from all of their roles
            foreach (var uid in userIds)
            {
                var rids = memberships[uid];
                var moduleToMask = new Dictionary<int, int>();

                foreach (var rid in rids)
                {
                    if (!byRole.TryGetValue(rid, out var mp)) continue;
                    foreach (var kv in mp)
                        moduleToMask[kv.Key] = moduleToMask.TryGetValue(kv.Key, out var cur)
                            ? (cur | kv.Value)
                            : kv.Value;
                }

                // wipe old permission user-claims and write new
                var oldUserClaims = await _db.Set<IdentityUserClaim<string>>()
                    .Where(uc => uc.UserId == uid && uc.ClaimType == PermissionClaimType)
                    .ToListAsync(ct);
                _db.RemoveRange(oldUserClaims);

                foreach (var (mod, mask) in moduleToMask)
                {
                    _db.Add(new IdentityUserClaim<string>
                    {
                        UserId = uid,
                        ClaimType = PermissionClaimType,
                        ClaimValue = $"{mod}:{PermissionExtensions.NormalizeInt(mask)}"
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}
