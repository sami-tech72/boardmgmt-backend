// Application/Roles/Queries/GetRoles/GetRolesQueryHandler.cs
using BoardMgmt.Application.Common.Interfaces;
using MediatR;

namespace BoardMgmt.Application.Roles.Queries.GetRoles
{
    public sealed class GetRolesQueryHandler(IRoleService roles, IRolePermissionStore store)
        : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleListItem>>
    {
        public async Task<IReadOnlyList<RoleListItem>> Handle(GetRolesQuery request, CancellationToken ct)
        {
            var all = await roles.GetAllAsync(ct); // (Id, Name)
            if (all.Count == 0) return Array.Empty<RoleListItem>();

            var permMap = await store.GetAggregatedForRolesAsync(all.Select(x => x.Id), ct);
            var list = all.Select(r =>
            {
                var dict = permMap.TryGetValue(r.Id, out var m) ? new Dictionary<int, int>(m) : new();
                return new RoleListItem(r.Id, r.Name, dict);
            }).ToList();

            return list;
        }
    }
}
