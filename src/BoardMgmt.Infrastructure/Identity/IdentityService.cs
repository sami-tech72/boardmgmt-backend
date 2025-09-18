using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;



namespace BoardMgmt.Infrastructure.Identity
{
    public class IdentityService(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        : IIdentityService, IIdentityServiceWithRoleAssign
    {
        public async Task<(bool Success, string UserId, IEnumerable<string> Errors)> RegisterUserAsync(
            string email, string password, string firstName, string lastName)
        {
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                return (false, string.Empty, result.Errors.Select(e => e.Description));

            // Default role - must exist in your seeding
            if (await roleManager.RoleExistsAsync("BoardMember"))
                await userManager.AddToRoleAsync(user, "BoardMember");

            return (true, user.Id, Array.Empty<string>());
        }

        public Task<AppUser?> FindUserByEmailAsync(string email)
            => userManager.Users.FirstOrDefaultAsync(u => u.Email == email);

        public Task<bool> CheckPasswordAsync(AppUser user, string password)
            => userManager.CheckPasswordAsync(user, password);

        public async Task<IEnumerable<string>> GetUserRolesAsync(AppUser user)
            => await userManager.GetRolesAsync(user);

        public async Task<bool> AddUserToRoleAsync(string userId, string roleName)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return false;
            var res = await userManager.AddToRoleAsync(user, roleName);
            return res.Succeeded;
        }
    }
}
