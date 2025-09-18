// Application/Roles/Queries/GetRoles/GetRolesQuery.cs
using MediatR;

namespace BoardMgmt.Application.Roles.Queries.GetRoles
{
    public sealed record RoleListItem(string Id, string Name, Dictionary<int, int> Permissions);
    public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleListItem>>;
}
