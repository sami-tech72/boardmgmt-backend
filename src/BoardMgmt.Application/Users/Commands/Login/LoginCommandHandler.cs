// backend/src/BoardMgmt.Application/Users/Commands/Login/LoginCommandHandler.cs
using System.Security.Claims;
using System.Linq;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Roles.Commands.SetRolePermissions; // PermissionDto
using BoardMgmt.Domain.Identity;
using MediatR;

namespace BoardMgmt.Application.Users.Commands.Login;

public class LoginCommandHandler(
    IIdentityService identityService,
    IJwtTokenService jwtTokenService,
    IRoleService roleService
) : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await identityService.FindUserByEmailAsync(request.Email);
        if (user == null)
        {
            return new LoginResponse(string.Empty, string.Empty, request.Email, string.Empty,
                false, new[] { "Invalid email or password." });
        }

        var passwordValid = await identityService.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return new LoginResponse(string.Empty, string.Empty, request.Email, string.Empty,
                false, new[] { "Invalid email or password." });
        }

        var roles = (await identityService.GetUserRolesAsync(user)).ToList();

        // Build per-module permission claims: perm:{moduleKey} = normalized int mask
        var extraClaims = new List<Claim>();

        if (roles.Count > 0)
        {
            var roleId = await roleService.GetRoleIdByNameAsync(roles[0], ct);
            if (!string.IsNullOrWhiteSpace(roleId))
            {
                var perModule = await roleService.GetRolePermissionsAsync(roleId!, ct);
                foreach (PermissionDto row in perModule)
                {
                    var moduleKey = Convert.ToInt32(row.Module).ToString(); // "1","2",...
                    var allowedInt = (int)row.Allowed;                      // already normalized
                    extraClaims.Add(new Claim($"perm:{moduleKey}", allowedInt.ToString()));
                }
            }
        }

        var token = jwtTokenService.CreateToken(user.Id, user.Email!, roles, extraClaims);

        return new LoginResponse(
            token, user.Id, user.Email!, $"{user.FirstName} {user.LastName}",
            true, Array.Empty<string>());
    }
}
