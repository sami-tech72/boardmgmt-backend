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



        // -----------------------------
        // NEW: Profile / Account updates
        // -----------------------------

        public async Task<bool> UpdateUserNameAsync(string userId, string firstName, string lastName)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return false;

            user.FirstName = firstName ?? string.Empty;
            user.LastName = lastName ?? string.Empty;

            var res = await userManager.UpdateAsync(user);
            return res.Succeeded;
        }

        public async Task<(bool Success, string? Error)> UpdateEmailAsync(string userId, string newEmail)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return (false, "User not found.");

            // If your app uses email as username, keep them in sync.
            // Identity has dedicated setters to ensure normalization.
            var setEmailRes = await userManager.SetEmailAsync(user, newEmail);
            if (!setEmailRes.Succeeded)
                return (false, string.Join("; ", setEmailRes.Errors.Select(e => e.Description)));

            var setUserNameRes = await userManager.SetUserNameAsync(user, newEmail);
            if (!setUserNameRes.Succeeded)
                return (false, string.Join("; ", setUserNameRes.Errors.Select(e => e.Description)));

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> SetPasswordAsync(string userId, string newPassword)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return (false, "User not found.");

            // Use reset token so we don't need the old password.
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetRes = await userManager.ResetPasswordAsync(user, token, newPassword);
            if (!resetRes.Succeeded)
                return (false, string.Join("; ", resetRes.Errors.Select(e => e.Description)));

            return (true, null);
        }

        public async Task<bool> SetActiveAsync(string userId, bool isActive)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return false;

            // Option A: use your own IsActive flag (recommended since you already store it)
            user.IsActive = isActive;
            var u1 = await userManager.UpdateAsync(user);
            if (!u1.Succeeded) return false;

            // Option B (optional): additionally leverage Identity lockout to enforce inactivity
            // When inactive, lock the account far in the future; when active, clear lockout.
            // Comment out if you don't use lockout in your app.
            if (!await userManager.GetLockoutEnabledAsync(user))
            {
                // Enable lockout if you plan to use Option B
                await userManager.SetLockoutEnabledAsync(user, true);
            }

            if (!isActive)
            {
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }
            else
            {
                await userManager.SetLockoutEndDateAsync(user, null);
                await userManager.ResetAccessFailedCountAsync(user);
            }

            return true;
        }

        public async Task<bool> AssignDepartmentAsync(string userId, Guid? departmentId)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return false;

            user.DepartmentId = departmentId;
            var res = await userManager.UpdateAsync(user);
            return res.Succeeded;
        }
    }
}
