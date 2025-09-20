// backend/src/BoardMgmt.Application/Common/Interfaces/IRoleService.cs
using BoardMgmt.Application.Roles.Commands.SetRolePermissions;
using BoardMgmt.Domain.Identity;

namespace BoardMgmt.Application.Common.Interfaces
{
    public interface IRoleService
    {
        Task<(bool ok, string? roleId, string[] errors)> CreateRoleAsync(string name, CancellationToken ct);
        Task<(bool ok, string[] errors)> DeleteRoleAsync(string roleId, CancellationToken ct);
        Task<(bool ok, string[] errors)> RenameRoleAsync(string roleId, string name, CancellationToken ct);

        Task<IReadOnlyList<(string Id, string Name)>> GetAllAsync(CancellationToken ct);
        Task<IReadOnlyList<string>> GetAllRoleNamesAsync(CancellationToken ct);

        // 🔹 Used by LoginCommandHandler
        Task<string?> GetRoleIdByNameAsync(string roleName, CancellationToken ct);

        // 🔹 MUST return PermissionDto (Module: AppModule, Allowed: Permission)
        Task<IReadOnlyList<PermissionDto>> GetRolePermissionsAsync(string roleId, CancellationToken ct);

        // 🔹 Used by SetRolePermissionsCommandHandler
        Task<IReadOnlyList<SavedRolePermission>> SetRolePermissionsAsync(
            string roleId,
            IEnumerable<(AppModule module, Permission allowed)> items,
            CancellationToken ct);

        Task<(bool ok, string[] errors)> UpdateRoleAndPermissionsAsync(
            string roleId,
            string name,
            IEnumerable<(AppModule module, Permission allowed)> items,
            CancellationToken ct);
    }
}
