using MediatR;

namespace BoardMgmt.Application.Roles.Commands.CreateRole
{
    public sealed record CreateRoleCommand(string Name) : IRequest<CreateRoleResult>;
    public sealed record CreateRoleResult(string Id, string Name);
}
