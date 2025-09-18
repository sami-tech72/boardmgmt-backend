// Application/Roles/Commands/DeleteRole/DeleteRoleCommand.cs
using MediatR;
public sealed record DeleteRoleCommand(string RoleId) : IRequest<bool>;
