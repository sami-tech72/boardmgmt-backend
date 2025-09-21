using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Linq;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities; // AppUser
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace BoardMgmt.Infrastructure.Auth
{
    public sealed class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        // If your JWT uses custom claim names for role IDs, add them here.
        private static readonly string[] RoleIdClaimTypes =
        [
            "role_id",           // one claim per role id
            "role_ids",          // repeated claim or array serialized as multiple claims
            "aspnet.role_id",
            "aspnet.role_ids"
        ];

        public CurrentUser(
            IHttpContextAccessor accessor,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _accessor = accessor;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        private ClaimsPrincipal User =>
            _accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

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

        // ---- RoleIds (fast path from token) ----
        public IReadOnlyList<string> RoleIds
        {
            get
            {
                // Try multiple claim types
                foreach (var ct in RoleIdClaimTypes)
                {
                    var vals = User.FindAll(ct)
                        .Select(c => c.Value)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToArray();
                    if (vals.Length > 0) return vals;
                }

                // Optional: single CSV claim
                var csv = User.FindFirst("role_ids_csv")?.Value;
                if (!string.IsNullOrWhiteSpace(csv))
                {
                    var split = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (split.Length > 0) return split;
                }

                // Not in token
                return Array.Empty<string>();
            }
        }

        // ---- Fallback (map role names -> IDs using Identity) ----
        public async Task<IReadOnlyList<string>> GetRoleIdsAsync(CancellationToken ct)
        {
            // If the token already has role IDs, prefer them.
            var fromToken = RoleIds;
            if (fromToken.Count > 0) return fromToken;

            // Otherwise, map from role names in the token to IDs via RoleManager.
            var roleNames = Roles;
            if (roleNames.Count == 0) return Array.Empty<string>();

            var ids = new List<string>(roleNames.Count);
            foreach (var name in roleNames)
            {
                ct.ThrowIfCancellationRequested();
                var role = await _roleManager.FindByNameAsync(name);
                if (role != null) ids.Add(role.Id);
            }
            return ids;
        }
    }
}
