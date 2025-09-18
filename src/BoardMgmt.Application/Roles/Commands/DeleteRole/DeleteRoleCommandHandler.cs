// Application/Roles/Commands/DeleteRole/DeleteRoleCommandHandler.cs
using BoardMgmt.Application.Common.Interfaces;
using MediatR;

public sealed class DeleteRoleCommandHandler(IRoleService roles)
    : IRequestHandler<DeleteRoleCommand, bool>
{
    public async Task<bool> Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        var (ok, errors) = await roles.DeleteRoleAsync(request.RoleId, ct);
        if (!ok) throw new InvalidOperationException(string.Join("; ", errors));
        return true;
    }
}
