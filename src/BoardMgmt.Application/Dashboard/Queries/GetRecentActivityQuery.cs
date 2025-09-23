using MediatR;
using BoardMgmt.Application.Dashboard.DTOs;
using BoardMgmt.Application.Common.Interfaces.Repositories;

namespace BoardMgmt.Application.Dashboard.Queries;

public record GetRecentActivityQuery(int Take = 10) : IRequest<IReadOnlyList<DashboardActivityDto>>;

public class GetRecentActivityQueryHandler : IRequestHandler<GetRecentActivityQuery, IReadOnlyList<DashboardActivityDto>>
{
    private readonly IActivityReadRepository _repo;
    public GetRecentActivityQueryHandler(IActivityReadRepository repo) => _repo = repo;

    public Task<IReadOnlyList<DashboardActivityDto>> Handle(GetRecentActivityQuery request, CancellationToken ct)
        => _repo.GetRecentAsync(request.Take, ct);
}
