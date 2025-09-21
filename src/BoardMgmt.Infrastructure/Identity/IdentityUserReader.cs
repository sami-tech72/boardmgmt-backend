// File: src/BoardMgmt.Infrastructure/Identity/IdentityUserReader.cs
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity; // ✅ AppUser
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Security.Claims;

public class IdentityUserReader : IIdentityUserReader
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;   // ✅ add RoleManager
    private readonly IHttpContextAccessor _http;

    public IdentityUserReader(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,                // ✅ inject
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _http = httpContextAccessor;
    }

    public async Task<IReadOnlyList<AppUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var list = new List<AppUser>();
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var u = await _userManager.FindByIdAsync(id);
            if (u is not null) list.Add(u);
        }
        return list;
    }

    public async Task<IReadOnlyList<string>> GetCurrentUserRoleNamesAsync(CancellationToken ct)
    {
        var userId = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Array.Empty<string>();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return Array.Empty<string>();

        var names = await _userManager.GetRolesAsync(user);    // role names
        return names.ToList();
    }

    public async Task<IReadOnlyList<string>> GetCurrentUserRoleIdsAsync(CancellationToken ct)
    {
        // 1) Get names
        var names = await GetCurrentUserRoleNamesAsync(ct);
        if (names.Count == 0) return Array.Empty<string>();

        // 2) Map names -> IDs in one DB trip
        var ids = _roleManager.Roles
            .Where(r => names.Contains(r.Name!))
            .Select(r => r.Id)
            .ToList();

        return ids;
    }
}
