using BoardMgmt.Application.Common.Interfaces;
using MediatR;

namespace BoardMgmt.Application.Roles.Commands.RenameRoleWithPermissions
{
    public sealed class RenameRoleWithPermissionsCommandHandler(IRoleService roles)
        : IRequestHandler<RenameRoleWithPermissionsCommand, bool>
    {
        public async Task<bool> Handle(RenameRoleWithPermissionsCommand request, CancellationToken ct)
        {
            var tuples = request.Items.Select(i => (i.Module, i.Allowed));
            var (ok, errors) = await roles.UpdateRoleAndPermissionsAsync(request.RoleId, request.Name, tuples, ct);

            if (!ok) throw new InvalidOperationException(string.Join("; ", errors));
            return true;
        }
    }
}
