using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Roles.Commands.AssignRole;
using MediatR;

namespace BoardMgmt.Application.Users.Commands.Register;

public class RegisterCommandHandler(
    IIdentityService identityService,
    ISender mediator
) : IRequestHandler<RegisterCommand, RegisterResponse>
{
    public async Task<RegisterResponse> Handle(RegisterCommand request, CancellationToken ct)
    {
        var (success, userId, errors) = await identityService.RegisterUserAsync(
            request.Email, request.Password, request.FirstName, request.LastName);

        if (!success || string.IsNullOrWhiteSpace(userId))
            return new(userId!, request.Email, request.FirstName, request.LastName, success, errors);

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            // Assign; handler validates existence in RoleManager
            var assign = await mediator.Send(new AssignRoleCommand(userId, new[] { request.Role! }), ct);
            if (!assign.Success)
            {
                var merged = errors?.ToList() ?? new List<string>();
                merged.AddRange(assign.Errors);
                return new(userId, request.Email, request.FirstName, request.LastName, false, merged);
            }
        }

        return new(userId, request.Email, request.FirstName, request.LastName, true, errors);
    }
}
