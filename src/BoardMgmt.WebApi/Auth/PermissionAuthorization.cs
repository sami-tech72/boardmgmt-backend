using System.Security.Claims;
using BoardMgmt.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace BoardMgmt.WebApi.Auth
{
    // A simple requirement that says:
    // "User must have perm:{ModuleKey} claim with the Needed bit set"
    public sealed record PermissionRequirement(string ModuleKey, Permission Needed) : IAuthorizationRequirement;

    // Correctly derive from AuthorizationHandler<TRequirement>
    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        // ✅ Must override HandleRequirementAsync (not HandleAsync)
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            // Example claim type: "perm:9" => "17"
            var claimType = $"perm:{requirement.ModuleKey}";
            var claim = context.User.FindFirst(claimType);

            if (claim is not null && int.TryParse(claim.Value, out var maskInt))
            {
                var mask = ((Permission)maskInt).Normalize(); // defensive: ensure Page is implied
                // Bitwise include check
                if ((mask & requirement.Needed) == requirement.Needed)
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
