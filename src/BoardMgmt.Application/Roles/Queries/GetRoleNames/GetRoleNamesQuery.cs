using MediatR;

namespace BoardMgmt.Application.Roles.Queries.GetRoleNames;

public sealed record GetRoleNamesQuery : IRequest<IReadOnlyList<string>>;
