using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BoardMgmt.Application.Common.Interfaces
{
    /// Read-side helper that returns aggregated bitmasks per module.
    public interface IRolePermissionStore
    {
        /// Single role → { moduleId -> allowedBitmask }
        Task<IDictionary<int, int>> GetAggregatedForRoleAsync(string roleId, CancellationToken ct);

        /// Many roles → { roleId -> { moduleId -> allowedBitmask } }
        Task<IDictionary<string, IDictionary<int, int>>> GetAggregatedForRolesAsync(
            IEnumerable<string> roleIds,
            CancellationToken ct);
    }
}
