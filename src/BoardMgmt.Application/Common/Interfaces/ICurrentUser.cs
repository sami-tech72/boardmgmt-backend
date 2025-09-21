using System.Security.Claims;

namespace BoardMgmt.Application.Common.Interfaces
{
    public interface ICurrentUser
    {
        string? UserId { get; }
        string? Email { get; }
        IReadOnlyList<string> Roles { get; }
        bool IsAuthenticated { get; }
        ClaimsPrincipal Principal { get; }

        bool IsInRole(string role);
        string? GetClaim(string claimType);

        // From token (fast path if emitted)
        IReadOnlyList<string> RoleIds { get; }

        // Fallback that can hit the DB if RoleIds are not in token
        Task<IReadOnlyList<string>> GetRoleIdsAsync(CancellationToken ct);


    }
}
