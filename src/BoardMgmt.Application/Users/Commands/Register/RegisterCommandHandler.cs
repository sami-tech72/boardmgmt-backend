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

        var mergedErrors = errors?.ToList() ?? new List<string>();

        // Assign role if provided
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var assign = await mediator.Send(new AssignRoleCommand(userId, new[] { request.Role! }), ct);
            if (!assign.Success)
                mergedErrors.AddRange(assign.Errors);
        }

        // Assign department if provided
        if (request.DepartmentId.HasValue)
        {
            var ok = await mediator.Send(new AssignDepartmentCommand(userId, request.DepartmentId), ct);
            if (!ok)
                mergedErrors.Add("Failed to assign department.");
        }

        var finalSuccess = !mergedErrors.Any();

        return new(userId, request.Email, request.FirstName, request.LastName, finalSuccess, mergedErrors);
    }
}

