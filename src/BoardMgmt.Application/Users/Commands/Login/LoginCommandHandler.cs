using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using MediatR;

namespace BoardMgmt.Application.Users.Commands.Login;

public class LoginCommandHandler(
    IIdentityService identityService,
    IJwtTokenService jwtTokenService)
    : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await identityService.FindUserByEmailAsync(request.Email);
        if (user == null)
        {
            return new LoginResponse(
                string.Empty, string.Empty, request.Email, string.Empty,
                false, ["Invalid email or password."]);
        }

        var passwordValid = await identityService.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return new LoginResponse(
                string.Empty, string.Empty, request.Email, string.Empty,
                false, ["Invalid email or password."]);
        }

        var roles = await identityService.GetUserRolesAsync(user);
        var token = jwtTokenService.CreateToken(user.Id, user.Email!, roles);

        return new LoginResponse(
            token, user.Id, user.Email!, $"{user.FirstName} {user.LastName}",
            true, []);
    }
}