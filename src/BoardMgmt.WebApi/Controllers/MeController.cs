using BoardMgmt.Domain.Entities;
using BoardMgmt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly RoleManager<IdentityRole> _roles;

    public MeController(AppDbContext db, UserManager<AppUser> users, RoleManager<IdentityRole> roles)
    { _db = db; _users = users; _roles = roles; }

    // Returns: { [moduleId:number]: flagsNumber }  ← exactly what AccessService expects
    [HttpGet("permissions")]
    public async Task<ActionResult<Dictionary<int, int>>> Permissions()
    {
        var me = await _users.GetUserAsync(User);
        if (me == null) return Unauthorized();

        var roleNames = await _users.GetRolesAsync(me);
        var roleIds = await _roles.Roles.Where(r => roleNames.Contains(r.Name!)).Select(r => r.Id).ToListAsync();

        var rows = await _db.RolePermissions.Where(p => roleIds.Contains(p.RoleId)).ToListAsync();

        var dict = rows
            .GroupBy(p => (int)p.Module)
            .ToDictionary(
                g => g.Key,
                g => g.Aggregate(0, (acc, p) => acc | (int)p.Allowed));

        return dict;
    }
}
