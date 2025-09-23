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

        // NEW used by Update handler:
        Task<bool> UpdateUserNameAsync(string userId, string firstName, string lastName);
        Task<(bool Success, string? Error)> UpdateEmailAsync(string userId, string newEmail);
        Task<(bool Success, string? Error)> SetPasswordAsync(string userId, string newPassword);
        Task<bool> SetActiveAsync(string userId, bool isActive);
        Task<bool> AssignDepartmentAsync(string userId, Guid? departmentId);
    }

    // Used by AssignRole command
    public interface IIdentityServiceWithRoleAssign
    {
        Task<bool> AddUserToRoleAsync(string userId, string roleName);
    }
}
