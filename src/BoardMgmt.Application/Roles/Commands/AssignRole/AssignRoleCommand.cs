using MediatR;

namespace BoardMgmt.Application.Roles.Commands.AssignRole;

public sealed record AssignRoleCommand(
    string UserId,
    IReadOnlyCollection<string>? Roles // role NAMES
) : IRequest<AssignRoleResult>;

public sealed record AssignRoleResult(
    bool Success,
    IReadOnlyCollection<string> AppliedRoles,
    IReadOnlyCollection<string> Errors
);
