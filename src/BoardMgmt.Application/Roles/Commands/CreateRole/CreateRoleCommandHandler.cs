using BoardMgmt.Application.Common.Interfaces;
using MediatR;

namespace BoardMgmt.Application.Roles.Commands.CreateRole
{
    public sealed class CreateRoleCommandHandler(IRoleService roles)
        : IRequestHandler<CreateRoleCommand, CreateRoleResult>
    {
        public async Task<CreateRoleResult> Handle(CreateRoleCommand request, CancellationToken ct)
        {
            var (ok, roleId, errors) = await roles.CreateRoleAsync(request.Name, ct);
            if (!ok || string.IsNullOrWhiteSpace(roleId))
                throw new InvalidOperationException(string.Join("; ", errors));

            return new CreateRoleResult(roleId!, request.Name);
        }
    }
}
