using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BoardMgmt.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BoardMgmt.Infrastructure.Auth
{
    public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

        public string? UserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        public string? Email =>
            User.FindFirstValue(ClaimTypes.Email) ??
            User.FindFirstValue(JwtRegisteredClaimNames.Email);

        public IReadOnlyList<string> Roles =>
            User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

        public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;

        public ClaimsPrincipal Principal => User;

        public bool IsInRole(string role) => User.IsInRole(role);

        public string? GetClaim(string claimType) => User.FindFirstValue(claimType);
    }
}
