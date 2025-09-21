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
            // ---- 1) Try fast path from claims on the principal (JWT / user claims)
            // Format: type="permission", value = "<moduleInt>:<maskInt>"
            if (int.TryParse(requirement.ModuleId, out var moduleInt))
            {
                var claim = context.User.FindAll(ClaimType)
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

                if (claim is int maskFromClaim &&
                    (((Permission)maskFromClaim) & requirement.Needed) == requirement.Needed)
                {
                    context.Succeed(requirement);
                    return;
                }
            }

            // ---- 2) Authoritative check from DB (always reflects latest changes)
            var module = (AppModule)moduleInt;
            if (await perms.HasMineAsync(module, requirement.Needed, CancellationToken.None))
            {
                context.Succeed(requirement);
            }
        }
    }
}
