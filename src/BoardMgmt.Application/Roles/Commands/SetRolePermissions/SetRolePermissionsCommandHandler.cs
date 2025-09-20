using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Identity;
using MediatR;

namespace BoardMgmt.Application.Roles.Commands.SetRolePermissions
{
    public sealed class SetRolePermissionsCommandHandler(IRoleService roles)
        : IRequestHandler<SetRolePermissionsCommand, IReadOnlyList<SavedRolePermission>>
    {
        public Task<IReadOnlyList<SavedRolePermission>> Handle(SetRolePermissionsCommand request, CancellationToken ct)
        {
            // Application-level safety
            var normalized = request.Items.Select(i => (i.Module, i.Allowed.Normalize()));
            return roles.SetRolePermissionsAsync(request.RoleId, normalized, ct);
        }
    }
}
