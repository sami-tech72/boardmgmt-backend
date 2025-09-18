using MediatR;

namespace BoardMgmt.Application.Roles.Queries.GetRoles
{
    public sealed record GetRolesQuery() : IRequest<IReadOnlyList<string>>;
}
