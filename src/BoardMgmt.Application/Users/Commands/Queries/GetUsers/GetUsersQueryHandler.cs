using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

public sealed class GetUsersQueryHandler(UserManager<AppUser> userManager, IAppDbContext db)
  : IRequestHandler<GetUsersQuery, UsersPage>
{
    public async Task<UsersPage> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var users = await userManager.Users.AsNoTracking().ToListAsync(ct);

        // Cache departments (id -> name)
        var deptMap = await db.Departments.AsNoTracking()
                           .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var dtos = new List<UserDto>(users.Count);
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            var isActive = !(u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow);

            var fullName = TryGetString(u, "FullName");
            if (string.IsNullOrWhiteSpace(fullName))
            {
                var first = TryGetString(u, "FirstName");
                var last = TryGetString(u, "LastName");
                fullName = $"{first} {last}".Trim();
            }
            if (string.IsNullOrWhiteSpace(fullName))
                fullName = u.UserName ?? u.Email ?? "User";

            Guid? deptId = u.DepartmentId; // strong-typed from AppUser
            string? deptName = (deptId.HasValue && deptMap.TryGetValue(deptId.Value, out var nm)) ? nm : null;

            dtos.Add(new UserDto(
                u.Id,
                fullName,
                u.Email ?? string.Empty,
                roles.ToList(),
                isActive,
                deptId,
                deptName
            ));
        }

        IEnumerable<UserDto> filtered = dtos;

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var q = request.Q.Trim();

            filtered = filtered.Where(u =>
                (u.FullName ?? string.Empty).Contains(q) ||
                (u.Email ?? string.Empty).Contains(q));
        }


        if (request.ActiveOnly == true)
            filtered = filtered.Where(u => u.IsActive);

        if (request.Roles is { Count: > 0 })
        {
            var wanted = request.Roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            filtered = filtered.Where(u => u.Roles.Any(r => wanted.Contains(r)));
        }

        if (request.DepartmentId.HasValue)
            filtered = filtered.Where(u => u.DepartmentId == request.DepartmentId.Value);

        var total = filtered.Count();

        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 500);
        var items = filtered.OrderBy(u => u.FullName)
                            .Skip((page - 1) * size)
                            .Take(size)
                            .ToList();

        return new UsersPage(items, total);
    }

    private static string TryGetString(object obj, string propName)
    {
        var p = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var val = p?.GetValue(obj) as string;
        return val ?? string.Empty;
    }
}
