using BoardMgmt.Application.Common.Interfaces;
using MediatR;

namespace BoardMgmt.Application.Roles.Commands.CreateRole
{
    public sealed class CreateRoleCommandHandler(IRoleService roles) : IRequestHandler<CreateRoleCommand, bool>
    {
        public async Task<bool> Handle(CreateRoleCommand request, CancellationToken ct)
        {
            if (await roles.RoleExistsAsync(request.Name, ct)) return true;
            var (ok, _) = await roles.CreateRoleAsync(request.Name, ct);
            return ok;
        }
    }
}
