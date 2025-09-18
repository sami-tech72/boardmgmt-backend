using BoardMgmt.Domain.Identity;

namespace BoardMgmt.Application.Common.Interfaces
{
    public interface IPermissionService
    {
        Task<Permission> GetMineAsync(AppModule module, CancellationToken ct);
        Task<bool> HasMineAsync(AppModule module, Permission needed, CancellationToken ct);
        Task EnsureMineAsync(AppModule module, Permission needed, CancellationToken ct); // throws if missing
    }
}
