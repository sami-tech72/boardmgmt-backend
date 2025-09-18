using BoardMgmt.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;



namespace BoardMgmt.Infrastructure.Identity
{
    public class RoleService(RoleManager<IdentityRole> roles) : IRoleService
    {
        public Task<bool> RoleExistsAsync(string name, CancellationToken ct) => roles.RoleExistsAsync(name);

        public async Task<(bool Succeeded, string[] Errors)> CreateRoleAsync(string name, CancellationToken ct)
        {
            var res = await roles.CreateAsync(new IdentityRole(name));
            return res.Succeeded ? (true, Array.Empty<string>()) : (false, res.Errors.Select(e => e.Description).ToArray());
        }

        public async Task<IReadOnlyList<string>> GetAllRoleNamesAsync(CancellationToken ct) =>
            await roles.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync(ct);
    }
}
