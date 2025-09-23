// /Application/Users/Commands/Update/UpdateUserCommandHandler.cs
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Roles.Commands.AssignRole;
using BoardMgmt.Application.Users.Commands.Register;
using MediatR;

namespace BoardMgmt.Application.Users.Commands.Update;

public class UpdateUserCommandHandler(
    IIdentityService identity,
    ISender mediator
) : IRequestHandler<UpdateUserCommand, UpdateUserResponse>
{
    public async Task<UpdateUserResponse> Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var errors = new List<string>();
        var userId = request.UserId;

        // Basic profile updates
        if (!string.IsNullOrWhiteSpace(request.FirstName) || !string.IsNullOrWhiteSpace(request.LastName))
        {
            var ok = await identity.UpdateUserNameAsync(userId, request.FirstName ?? "", request.LastName ?? "");
            if (!ok) errors.Add("Failed to update name.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var (ok, err) = await identity.UpdateEmailAsync(userId, request.Email!);
            if (!ok) errors.Add(err ?? "Failed to update email.");
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var (ok, err) = await identity.SetPasswordAsync(userId, request.NewPassword!);
            if (!ok) errors.Add(err ?? "Failed to update password.");
        }

        // Role (replace primary role)
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var assign = await mediator.Send(new AssignRoleCommand(userId, new[] { request.Role! }), ct);
            if (!assign.Success) errors.AddRange(assign.Errors);
        }

        // Department
        if (request.DepartmentId.HasValue)
        {
            var ok = await mediator.Send(new AssignDepartmentCommand(userId, request.DepartmentId), ct);
            if (!ok) errors.Add("Failed to assign department.");
        }

        // Active flag
        if (request.IsActive.HasValue)
        {
            var ok = await identity.SetActiveAsync(userId, request.IsActive.Value);
            if (!ok) errors.Add("Failed to update active status.");
        }

        var success = errors.Count == 0;
        return new(userId, success, errors);
    }
}
