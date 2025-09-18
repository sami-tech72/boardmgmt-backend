using BoardMgmt.Domain.Entities;

namespace BoardMgmt.Application.Common.Interfaces
{
    public interface IIdentityService
    {
        Task<(bool Success, string UserId, IEnumerable<string> Errors)> RegisterUserAsync(
            string email, string password, string firstName, string lastName);

        Task<AppUser?> FindUserByEmailAsync(string email);
        Task<bool> CheckPasswordAsync(AppUser user, string password);
        Task<IEnumerable<string>> GetUserRolesAsync(AppUser user);
    }

    // Used by AssignRole command
    public interface IIdentityServiceWithRoleAssign
    {
        Task<bool> AddUserToRoleAsync(string userId, string roleName);
    }
}
