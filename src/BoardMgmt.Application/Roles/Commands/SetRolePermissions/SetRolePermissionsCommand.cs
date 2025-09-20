using BoardMgmt.Domain.Identity;
using MediatR;

namespace BoardMgmt.Application.Roles.Commands.SetRolePermissions
{
    // Use your own AppModule type (enum/int). This matches your existing code.
    public sealed record PermissionDto(AppModule Module, Permission Allowed);
    public sealed record SavedRolePermission(Guid Id, AppModule Module, Permission Allowed);

    public sealed record SetRolePermissionsCommand(
        string RoleId,
        List<PermissionDto> Items
    ) : IRequest<IReadOnlyList<SavedRolePermission>>;
}
