namespace BoardMgmt.Application.Common.Interfaces
{
    public interface IRoleService
    {
        Task<bool> RoleExistsAsync(string name, CancellationToken ct);
        Task<(bool Succeeded, string[] Errors)> CreateRoleAsync(string name, CancellationToken ct);
        Task<IReadOnlyList<string>> GetAllRoleNamesAsync(CancellationToken ct);
    }
}
