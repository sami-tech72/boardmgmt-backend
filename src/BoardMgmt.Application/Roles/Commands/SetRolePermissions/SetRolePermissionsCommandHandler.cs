using BoardMgmt.Application.Common.Interfaces;
using MediatR;

namespace BoardMgmt.Application.Roles.Commands.SetRolePermissions
{
    public sealed class SetRolePermissionsCommandHandler(IRoleService roles)
        : IRequestHandler<SetRolePermissionsCommand, IReadOnlyList<SavedRolePermission>>
    {
        public Task<IReadOnlyList<SavedRolePermission>> Handle(SetRolePermissionsCommand request, CancellationToken ct)
        {
            var tuples = request.Items.Select(i => (i.Module, i.Allowed));
            return roles.SetRolePermissionsAsync(request.RoleId, tuples, ct);
        }
    }
}
