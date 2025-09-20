using MediatR;

namespace BoardMgmt.Application.Users.Queries.GetUsers;

public sealed record GetUsersQuery : IRequest<IReadOnlyList<UserDto>>;

public sealed record UserDto(
    string Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsActive
);
