using BoardMgmt.Domain.Entities; // AppUser
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Users.Queries.GetUsers;

public sealed class GetUsersQueryHandler(
    UserManager<AppUser> userManager
) : IRequestHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        // Fetch minimal identity fields
        var users = await userManager.Users.AsNoTracking().ToListAsync(ct);

        var list = new List<UserDto>(users.Count);
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            var isActive = u.LockoutEnd is null || u.LockoutEnd <= DateTimeOffset.UtcNow;
            var fullName = (u.GetType().GetProperty("FullName")?.GetValue(u) as string)
                           ?? string.Join(' ',
                                 (u.GetType().GetProperty("FirstName")?.GetValue(u) as string) ?? "",
                                 (u.GetType().GetProperty("LastName")?.GetValue(u) as string) ?? "").Trim();

            fullName = string.IsNullOrWhiteSpace(fullName) ? (u.UserName ?? u.Email ?? "User") : fullName;

            list.Add(new UserDto(
                Id: u.Id,
                FullName: fullName,
                Email: u.Email ?? "",
                Roles: roles.ToList(),
                IsActive: isActive
            ));
        }

        return list;
    }
}
