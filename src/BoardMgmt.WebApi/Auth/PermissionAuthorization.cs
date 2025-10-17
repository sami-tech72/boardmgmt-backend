using System.Security.Claims;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace BoardMgmt.WebApi.Auth
{
    /// A single policy = (module, neededPermission).
    public sealed class PermissionRequirement(string moduleId, Permission needed) : IAuthorizationRequirement
    {
        public string ModuleId { get; } = moduleId;   // e.g. "1" for Users
        public Permission Needed { get; } = needed;
    }

    /// Checks (a) fast path from claims if present, else (b) DB via IPermissionService.
    public sealed class PermissionAuthorizationHandler(IPermissionService perms)
        : AuthorizationHandler<PermissionRequirement>
    {

        private const string ClaimType = "permission"; // we store "permission" claims

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var hasModuleInt = int.TryParse(requirement.ModuleId, out var moduleInt);
            // ---- 1) Parse permission claim (if any) for observability / diagnostics
            bool? claimIndicatesAllowed = null;
            if (hasModuleInt)
            {
                var claimMask = context.User.FindAll(ClaimType)
                    .Select(c => c.Value)
                    .Select(v =>
                    {
                        var parts = v.Split(':', 2);
                        return parts.Length == 2
                               && int.TryParse(parts[0], out var mod)
                               && int.TryParse(parts[1], out var mask)
                            ? (ok: true, mod, mask)
                            : (ok: false, mod: 0, mask: 0);
                    })
                    .Where(t => t.ok && t.mod == moduleInt)
                    .Select(t => t.mask)
                    .Cast<int?>()
                    .FirstOrDefault();

                if (claimMask is int maskFromClaim)
                {
                    claimIndicatesAllowed = (((Permission)maskFromClaim) & requirement.Needed) == requirement.Needed;
                }
            }

            // ---- 2) Authoritative check from DB (always reflects latest changes)
            if (hasModuleInt)
            {
                var module = (AppModule)moduleInt;
                if (await perms.HasMineAsync(module, requirement.Needed, CancellationToken.None))
                {
                    context.Succeed(requirement);
                }
                else if (claimIndicatesAllowed == true)
                {
                    // Claims can become stale when role permissions change while a user is logged in.
                    // We intentionally do nothing here so the request is denied based on the DB result.
                }
            }
        }
    }
}
