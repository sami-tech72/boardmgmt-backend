// /Application/Users/Commands/AssignDepartment/AssignDepartmentCommandHandler.cs
using BoardMgmt.Application.Common.Interfaces;
using MediatR;

namespace BoardMgmt.Application.Users.Commands.Register;

public class AssignDepartmentCommandHandler(IIdentityService identity) : IRequestHandler<AssignDepartmentCommand, bool>
{
    public async Task<bool> Handle(AssignDepartmentCommand request, CancellationToken ct)
        => await identity.AssignDepartmentAsync(request.UserId, request.DepartmentId);
}
