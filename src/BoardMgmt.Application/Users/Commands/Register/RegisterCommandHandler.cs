using BoardMgmt.Application.Common.Interfaces;
using MediatR;

namespace BoardMgmt.Application.Users.Commands.Register;

public class RegisterCommandHandler(IIdentityService identityService)
    : IRequestHandler<RegisterCommand, RegisterResponse>
{
    public async Task<RegisterResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var (success, userId, errors) = await identityService.RegisterUserAsync(
            request.Email, request.Password, request.FirstName, request.LastName);

        return new RegisterResponse(
            userId, request.Email, request.FirstName, request.LastName,
            success, errors);
    }
}