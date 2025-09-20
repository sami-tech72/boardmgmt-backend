// backend/src/BoardMgmt.Infrastructure/Identity/RoleService.cs
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

        public RoleService(RoleManager<IdentityRole> roles, AppDbContext db)
        {
            _roles = roles;
            _db = db;
        }

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
            => await _roles.Roles.Select(r => new ValueTuple<string, string>(r.Id, r.Name!)).ToListAsync(ct);

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
            var olds = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
            _db.RolePermissions.RemoveRange(olds);
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
            var olds = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
            _db.RolePermissions.RemoveRange(olds);

            // 🔸 normalize before persisting
            var entities = items
                .Select(i => (i.module, allowed: i.allowed.Normalize()))
                .Where(i => i.allowed != Permission.None)
                .Select(i => new RolePermission
                {
                    RoleId = roleId,
                    Module = i.module,
                    Allowed = i.allowed
                })
                .ToList();

            if (entities.Count > 0)
                await _db.RolePermissions.AddRangeAsync(entities, ct);

            await _db.SaveChangesAsync(ct);

            return entities.Select(e => new SavedRolePermission(e.Id, e.Module, e.Allowed)).ToList();
        }

        public async Task<(bool ok, string[] errors)> UpdateRoleAndPermissionsAsync(
            string roleId,
            string name,
            IEnumerable<(AppModule module, Permission allowed)> items,
            CancellationToken ct)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var role = await _roles.FindByIdAsync(roleId);
                if (role is null) return (false, new[] { "Role not found." });

                role.Name = name;
                role.NormalizedName = name.ToUpperInvariant();
                var rename = await _roles.UpdateAsync(role);
                if (!rename.Succeeded)
                    return (false, rename.Errors.Select(e => e.Description).ToArray());

                var olds = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
                _db.RolePermissions.RemoveRange(olds);

                var ents = items
                    .Select(i => (i.module, allowed: i.allowed.Normalize()))
                    .Where(i => i.allowed != Permission.None)
                    .Select(i => new RolePermission
                    {
                        RoleId = roleId,
                        Module = i.module,
                        Allowed = i.allowed
                    })
                    .ToList();

                if (ents.Count > 0)
                    await _db.RolePermissions.AddRangeAsync(ents, ct);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return (true, Array.Empty<string>());
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                return (false, new[] { ex.Message });
            }
        }

        public async Task<IReadOnlyList<string>> GetAllRoleNamesAsync(CancellationToken ct) =>
            await _roles.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync(ct);

        // 🔹 Used by LoginCommandHandler
        public async Task<string?> GetRoleIdByNameAsync(string roleName, CancellationToken ct)
        {
            var role = await _roles.Roles
                .Where(r => r.Name == roleName)
                .Select(r => new { r.Id })
                .FirstOrDefaultAsync(ct);

            return role?.Id;
        }

        // 🔹 MUST return PermissionDto (NOT a tuple)
        public async Task<IReadOnlyList<PermissionDto>> GetRolePermissionsAsync(string roleId, CancellationToken ct)
        {
            var rows = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => new { rp.Module, rp.Allowed })
                .ToListAsync(ct);

            // defensive normalize on read, and project to PermissionDto
            var list = new List<PermissionDto>(rows.Count);
            foreach (var r in rows)
            {
                var normalizedInt = PermissionExtensions.NormalizeInt((int)r.Allowed);
                var normalizedPerm = (Permission)normalizedInt;

                // if rp.Module is stored as int → cast to AppModule
                var module = (AppModule)Convert.ToInt32(r.Module);

                list.Add(new PermissionDto(module, normalizedPerm));
            }

            return list;
        }
    }
}
