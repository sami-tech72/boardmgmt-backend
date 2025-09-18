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
    }
}
