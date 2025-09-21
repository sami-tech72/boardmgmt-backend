// backend/src/BoardMgmt.Application/Users/Commands/Login/LoginCommandHandler.cs
using System.Security.Claims;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Identity;
using MediatR;

namespace BoardMgmt.Application.Users.Commands.Login;

public class LoginCommandHandler(
    IIdentityService identityService,
    IJwtTokenService jwtTokenService,
    IRoleService roleService,
    IRolePermissionStore rolePermissionStore // 👈 add this
) : IRequestHandler<LoginCommand, LoginResponse>
{
    private const string PermissionClaimType = "permission"; // 👈 handler expects this

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await identityService.FindUserByEmailAsync(request.Email);
        if (user is null)
        {
            return new LoginResponse(
                string.Empty, string.Empty, request.Email, string.Empty,
                false, new[] { "Invalid email or password." });
        }

        var passwordValid = await identityService.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return new LoginResponse(
                string.Empty, string.Empty, request.Email, string.Empty,
                false, new[] { "Invalid email or password." });
        }

        // Roles (names) for token
        var roleNames = (await identityService.GetUserRolesAsync(user)).ToList();

        // ===== Aggregate permissions across ALL roles =====
        var roleIds = new List<string>();
        foreach (var rn in roleNames)
        {
            var rid = await roleService.GetRoleIdByNameAsync(rn, ct);
            if (!string.IsNullOrWhiteSpace(rid))
                roleIds.Add(rid!);
        }

        var extraClaims = new List<Claim>();

        if (roleIds.Count > 0)
        {
            // roleId -> { module -> mask }
            var agg = await rolePermissionStore.GetAggregatedForRolesAsync(roleIds, ct);

            // union per module across all roles
            var perModule = new Dictionary<int, int>();
            foreach (var kv in agg) // each role
            {
                foreach (var mm in kv.Value) // each module->mask for that role
                {
                    perModule[mm.Key] = perModule.TryGetValue(mm.Key, out var cur)
                        ? (cur | mm.Value)
                        : mm.Value;
                }
            }

            // emit "permission" claims with value "{module}:{mask}"
            foreach (var (moduleInt, rawMask) in perModule)
            {
                var normalized = PermissionExtensions.NormalizeInt(rawMask);
                extraClaims.Add(new Claim(PermissionClaimType, $"{moduleInt}:{normalized}"));
            }
        }

        var token = jwtTokenService.CreateToken(user.Id, user.Email!, roleNames, extraClaims);

        return new LoginResponse(
            token,
            user.Id,
            user.Email!,
            $"{user.FirstName} {user.LastName}",
            true,
            Array.Empty<string>());
    }
}
