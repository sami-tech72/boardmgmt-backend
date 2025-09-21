// File: src/BoardMgmt.Application/Common/Interfaces/IIdentityUserReader.cs
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity; // ✅ AppUser lives here

namespace BoardMgmt.Application.Common.Interfaces
{
    /// <summary>Read-only user lookup abstraction for Application layer.</summary>
    public interface IIdentityUserReader
    {
        Task<IReadOnlyList<AppUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct);

        /// <summary>
        /// Gets the current user's ASP.NET Role IDs (AspNetRoles.Id).
        /// </summary>
        Task<IReadOnlyList<string>> GetCurrentUserRoleIdsAsync(CancellationToken ct);

        /// <summary>
        /// (Optional convenience) Gets the current user's role names.
        /// </summary>
        Task<IReadOnlyList<string>> GetCurrentUserRoleNamesAsync(CancellationToken ct);
    }
}
