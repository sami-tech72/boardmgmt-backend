using BoardMgmt.Application.Common.Interfaces;
using MediatR;

namespace BoardMgmt.Application.Roles.Queries.GetRoles
{
    public sealed class GetRolesQueryHandler(IRoleService roles) : IRequestHandler<GetRolesQuery, IReadOnlyList<string>>
    {
        public Task<IReadOnlyList<string>> Handle(GetRolesQuery request, CancellationToken ct)
            => roles.GetAllRoleNamesAsync(ct);
    }
}
