using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;

public class IdentityUserReader : IIdentityUserReader
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IdentityUserReader(UserManager<AppUser> userManager, IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<AppUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var users = new List<AppUser>();
        foreach (var id in ids)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
                users.Add(user);
        }
        return users;
    }

    public async Task<IReadOnlyList<string>> GetCurrentUserRoleIdsAsync(CancellationToken ct)
    {
        var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Array.Empty<string>();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Array.Empty<string>();

        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToList();
    }
}
