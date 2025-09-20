using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Roles.Queries.GetRoleNames;

public sealed class GetRoleNamesQueryHandler(
    RoleManager<IdentityRole> roleManager
) : IRequestHandler<GetRoleNamesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetRoleNamesQuery request, CancellationToken ct)
    {
        return await roleManager.Roles
            .AsNoTracking()
            .Select(r => r.Name!)
            .ToListAsync(ct);
    }
}
