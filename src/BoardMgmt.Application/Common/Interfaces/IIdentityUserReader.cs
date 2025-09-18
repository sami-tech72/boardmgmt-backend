using BoardMgmt.Domain.Entities;

namespace BoardMgmt.Application.Common.Interfaces
{
    /// <summary>Read-only user lookup abstraction for Application layer.</summary>
    public interface IIdentityUserReader
    {
        Task<IReadOnlyList<AppUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct);
    }
}
