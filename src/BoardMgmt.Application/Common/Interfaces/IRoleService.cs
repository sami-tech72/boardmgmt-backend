using BoardMgmt.Application.Roles.Commands.SetRolePermissions;
using BoardMgmt.Domain.Identity;

namespace BoardMgmt.Application.Common.Interfaces
{
    public interface IRoleService
    {
        Task<bool> RoleExistsAsync(string name, CancellationToken ct);

        // Create and return role id (or existing id)
        Task<(bool ok, string? roleId, string[] errors)> CreateRoleAsync(string name, CancellationToken ct);

        // List roles (id,name) — used by GetRolesQuery
        Task<IReadOnlyList<(string Id, string Name)>> GetAllAsync(CancellationToken ct);

        // Rename only (still available if you use it elsewhere)
        Task<(bool ok, string[] errors)> RenameRoleAsync(string roleId, string name, CancellationToken ct);

        // Delete role (+ its permissions)
        Task<(bool ok, string[] errors)> DeleteRoleAsync(string roleId, CancellationToken ct);

        // Replace all permissions (kept for compatibility)
        Task<IReadOnlyList<SavedRolePermission>> SetRolePermissionsAsync(
            string roleId,
            IEnumerable<(AppModule module, Permission allowed)> items,
            CancellationToken ct);

        // ✅ NEW: rename + replace permissions in one DB transaction
        Task<(bool ok, string[] errors)> UpdateRoleAndPermissionsAsync(
            string roleId,
            string name,
            IEnumerable<(AppModule module, Permission allowed)> items,
            CancellationToken ct);

        // Optional helpers
        Task<IReadOnlyList<string>> GetAllRoleNamesAsync(CancellationToken ct);

        // ✅ NEW: read existing permissions for a role (for edit prefill)
        Task<IReadOnlyList<PermissionDto>> GetRolePermissionsAsync(string roleId, CancellationToken ct);
    }
}
