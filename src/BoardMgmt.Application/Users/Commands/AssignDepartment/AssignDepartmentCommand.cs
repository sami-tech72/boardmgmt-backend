// /Application/Users/Commands/AssignDepartment/AssignDepartmentCommand.cs
using MediatR;

namespace BoardMgmt.Application.Users.Commands.Register; // keep same ns next to RegisterCommand if you prefer

public record AssignDepartmentCommand(string UserId, Guid? DepartmentId) : IRequest<bool>;
