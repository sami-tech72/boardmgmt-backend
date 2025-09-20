using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Domain.Entities; // AppUser
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace BoardMgmt.Application.Roles.Commands.AssignRole;

public sealed class AssignRoleCommandHandler(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager
) : IRequestHandler<AssignRoleCommand, AssignRoleResult>
{
    private const bool EnforceSingleRole = true;

    public async Task<AssignRoleResult> Handle(AssignRoleCommand request, CancellationToken ct)
    {
        var errors = new List<string>();
        var applied = new List<string>();

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return new(false, Array.Empty<string>(), new[] { "User not found." });

        var incoming = (request.Roles ?? Array.Empty<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (EnforceSingleRole && incoming.Length > 1)
            errors.Add("Only one role may be assigned to a user.");

        // Validate roles exist in store
        foreach (var roleName in incoming)
            if (!await roleManager.RoleExistsAsync(roleName))
                errors.Add($"Role '{roleName}' does not exist.");

        if (errors.Count > 0)
            return new(false, Array.Empty<string>(), errors);

        var currentRoles = (await userManager.GetRolesAsync(user)).ToArray();
        var targetRoles = EnforceSingleRole ? incoming.Take(1).ToArray() : incoming;

        var toRemove = currentRoles.Where(r => !targetRoles.Contains(r, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (toRemove.Length > 0)
        {
            var res = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!res.Succeeded) errors.AddRange(res.Errors.Select(e => e.Description));
        }

        var toAdd = targetRoles.Where(r => !currentRoles.Contains(r, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (toAdd.Length > 0)
        {
            var res = await userManager.AddToRolesAsync(user, toAdd);
            if (!res.Succeeded) errors.AddRange(res.Errors.Select(e => e.Description));
        }

        if (errors.Count > 0)
            return new(false, Array.Empty<string>(), errors);

        applied.AddRange(targetRoles);
        return new(true, applied, Array.Empty<string>());
    }
}
