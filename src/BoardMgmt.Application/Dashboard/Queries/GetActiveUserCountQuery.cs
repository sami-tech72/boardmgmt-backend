// Application/Dashboard/Queries/GetActiveUserCountQuery.cs
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Application.Common.Interfaces.Repositories;
using MediatR;

namespace BoardMgmt.Application.Dashboard.Queries;

public sealed record GetActiveUserCountQuery : IRequest<int>;

public sealed class GetActiveUserCountQueryHandler
    : IRequestHandler<GetActiveUserCountQuery, int>
{
    private readonly IUserReadRepository _users;

    public GetActiveUserCountQueryHandler(IUserReadRepository users)
        => _users = users;

    public Task<int> Handle(GetActiveUserCountQuery request, CancellationToken ct)
        => _users.CountActiveAsync(ct);
}
