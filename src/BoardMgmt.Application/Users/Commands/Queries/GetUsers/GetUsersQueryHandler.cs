using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Domain.Entities; // AppUser

namespace BoardMgmt.Application.Users.Queries.GetUsers;

public sealed class GetUsersQueryHandler(
    UserManager<AppUser> userManager
) : IRequestHandler<GetUsersQuery, UsersPage>
{
    public async Task<UsersPage> Handle(GetUsersQuery request, CancellationToken ct)
    {
        // Pull minimal identity fields from DB
        var baseQuery = userManager.Users.AsNoTracking();

        // Materialize early since we use reflection for FullName and per-user role lookup
        var users = await baseQuery.ToListAsync(ct);

        // Map to DTO (compute FullName, Roles, IsActive)
        var dtos = new List<UserDto>(users.Count);
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            // Active if NOT locked out in the future
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

            dtos.Add(new UserDto(
                Id: u.Id,
                FullName: fullName,
                Email: u.Email ?? string.Empty,
                Roles: roles.ToList(),
                IsActive: isActive
            ));
        }

        // Filters (in-memory, OK for typical admin user counts)
        IEnumerable<UserDto> filtered = dtos;

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var q = request.Q.Trim().ToLowerInvariant();
            filtered = filtered.Where(u =>
                (u.FullName ?? string.Empty).ToLowerInvariant().Contains(q) ||
                (u.Email ?? string.Empty).ToLowerInvariant().Contains(q));
        }

        if (request.ActiveOnly.HasValue && request.ActiveOnly.Value)
        {
            filtered = filtered.Where(u => u.IsActive);
        }

        if (request.Roles is { Count: > 0 })
        {
            // match ANY requested role
            var wanted = request.Roles.Where(r => !string.IsNullOrWhiteSpace(r))
                                      .Select(r => r.Trim())
                                      .ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(u => u.Roles.Any(r => wanted.Contains(r)));
        }

        var total = filtered.Count();

        // Pagination
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var items = filtered
            .OrderBy(u => u.FullName) // stable order
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
