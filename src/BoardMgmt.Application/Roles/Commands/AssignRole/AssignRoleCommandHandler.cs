using BoardMgmt.Application.Common.Interfaces;
using MediatR;

namespace BoardMgmt.Application.Roles.Commands.AssignRole
{
    public sealed class AssignRoleCommandHandler(IIdentityService identity) : IRequestHandler<AssignRoleCommand, bool>
    {
        public async Task<bool> Handle(AssignRoleCommand request, CancellationToken ct)
        {
            if (identity is IIdentityServiceWithRoleAssign assigner)
                return await assigner.AddUserToRoleAsync(request.UserId, request.RoleName);

            return false;
        }
    }
}
