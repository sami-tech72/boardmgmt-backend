using MediatR;
using System.Collections.Generic;

namespace BoardMgmt.Application.Users.Queries.GetUsers;

public sealed record GetUsersQuery(
    string? Q = null,
    int Page = 1,
    int PageSize = 50,
    bool? ActiveOnly = null,
    IReadOnlyList<string>? Roles = null
) : IRequest<UsersPage>;

public sealed record UsersPage(
    IReadOnlyList<UserDto> Items,
    int Total
);

public sealed record UserDto(
    string Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsActive
);
