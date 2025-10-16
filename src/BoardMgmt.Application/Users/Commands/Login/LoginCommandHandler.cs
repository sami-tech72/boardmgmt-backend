// backend/src/BoardMgmt.Application/Users/Commands/Login/LoginCommandHandler.cs
using System.Security.Claims;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BoardMgmt.Application.Users.Commands.Login;

public class LoginCommandHandler(
    IIdentityService identityService,
    IJwtTokenService jwtTokenService,
    IRoleService roleService,
    IRolePermissionStore rolePermissionStore, // 👈 add this
    ILogger<LoginCommandHandler> logger
) : IRequestHandler<LoginCommand, LoginResponse>
{
    private const string PermissionClaimType = "permission"; // 👈 handler expects this
    private readonly ILogger<LoginCommandHandler> _logger = logger;

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Login attempt received for {Email}", request.Email);

        var user = await identityService.FindUserByEmailAsync(request.Email);
        if (user is null)
        {
            _logger.LogWarning("Login failed: user with email {Email} not found", request.Email);
            return new LoginResponse(
                string.Empty, string.Empty, request.Email, string.Empty,
                false, new[] { "Invalid email or password." });
        }

        var passwordValid = await identityService.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Login failed: invalid credentials supplied for {Email}", request.Email);
            return new LoginResponse(
                string.Empty, string.Empty, request.Email, string.Empty,
                false, new[] { "Invalid email or password." });
        }

        // Roles (names) for token
        var roleNames = (await identityService.GetUserRolesAsync(user)).ToList();

        if (roleNames.Count == 0)
        {
            _logger.LogInformation("User {Email} authenticated without any assigned roles", request.Email);
        }

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

        _logger.LogInformation("Login succeeded for {Email} with UserId {UserId}", request.Email, user.Id);

        return new LoginResponse(
            token,
            user.Id,
            user.Email!,
            $"{user.FirstName} {user.LastName}",
            true,
            Array.Empty<string>());
    }
}
