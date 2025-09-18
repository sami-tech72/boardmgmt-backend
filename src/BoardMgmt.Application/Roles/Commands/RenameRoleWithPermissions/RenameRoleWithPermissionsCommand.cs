using BoardMgmt.Domain.Identity;
using MediatR;

namespace BoardMgmt.Application.Roles.Commands.RenameRoleWithPermissions
{
    public sealed record PermissionDto(AppModule Module, Permission Allowed);

    public sealed record RenameRoleWithPermissionsCommand(
        string RoleId,
        string Name,
        List<PermissionDto> Items
    ) : IRequest<bool>;
}
