using MediatR;

namespace BoardMgmt.Application.Roles.Commands.AssignRole
{
    public sealed record AssignRoleCommand(string UserId, string RoleName) : IRequest<bool>;
}
