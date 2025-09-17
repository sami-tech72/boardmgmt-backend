using BoardMgmt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly AppDbContext _db;

    public UsersController(UserManager<AppUser> users, RoleManager<IdentityRole> roles, AppDbContext db)
    {
        _users = users;
        _roles = roles;
        _db = db;
    }

    public sealed record MinimalUserDto(string Id, string Name, string? Email, string[] Roles);
    public sealed record Paged<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

    /// GET /api/users/minimal?roles=BoardMember&roles=Secretary&q=jo&page=1&pageSize=50&activeOnly=true
    [HttpGet("minimal")]
    [Authorize]
    public async Task<ActionResult<Paged<MinimalUserDto>>> Minimal(
        [FromQuery] string[]? roles,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool activeOnly = true)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<AppUser> users = _users.Users;

        if (activeOnly)
        {
            users = users.Where(u => !u.LockoutEnabled || (u.LockoutEnd == null || u.LockoutEnd < DateTimeOffset.UtcNow));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim().ToLower();
            users = users.Where(u =>
                (u.UserName ?? "").ToLower().Contains(needle) ||
                (u.Email ?? "").ToLower().Contains(needle));
        }

        if (roles is { Length: > 0 })
        {
            var roleNames = roles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList();
            var roleIds = await _roles.Roles.Where(r => roleNames.Contains(r.Name!)).Select(r => r.Id).ToListAsync();

            var ur = _db.Set<IdentityUserRole<string>>();
            users =
                from u in users
                join x in ur on u.Id equals x.UserId
                where roleIds.Contains(x.RoleId)
                select u;

            users = users.Distinct();
        }

        var total = await users.CountAsync();

        var pageItems = await users
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new List<MinimalUserDto>(pageItems.Count);
        foreach (var u in pageItems)
        {
            var rs = (await _users.GetRolesAsync(u)).ToArray();
            result.Add(new MinimalUserDto(u.Id, u.UserName ?? (u.Email ?? "User"), u.Email, rs));
        }

        return Ok(new Paged<MinimalUserDto>(result, total, page, pageSize));
    }
}
