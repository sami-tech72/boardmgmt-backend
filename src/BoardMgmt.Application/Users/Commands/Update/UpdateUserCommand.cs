// /Application/Users/Commands/Update/UpdateUserCommand.cs
using MediatR;

namespace BoardMgmt.Application.Users.Commands.Update;

public record UpdateUserCommand(
    string UserId,          // ← FIX: include UserId
    string? FirstName,
    string? LastName,
    string? Email,
    string? NewPassword,
    string? Role,
    Guid? DepartmentId,
    bool? IsActive
) : IRequest<UpdateUserResponse>;

public record UpdateUserResponse(
    string UserId,
    bool Success,
    IEnumerable<string> Errors);
