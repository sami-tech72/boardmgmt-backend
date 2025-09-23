using MediatR;
using BoardMgmt.Application.Dashboard.DTOs;
using BoardMgmt.Application.Common.Interfaces.Repositories;

namespace BoardMgmt.Application.Dashboard.Queries;

public record GetRecentMeetingsQuery(int Take = 3) : IRequest<IReadOnlyList<DashboardMeetingDto>>;

public class GetRecentMeetingsQueryHandler : IRequestHandler<GetRecentMeetingsQuery, IReadOnlyList<DashboardMeetingDto>>
{
    private readonly IMeetingReadRepository _repo;
    public GetRecentMeetingsQueryHandler(IMeetingReadRepository repo) => _repo = repo;

    public Task<IReadOnlyList<DashboardMeetingDto>> Handle(GetRecentMeetingsQuery request, CancellationToken ct)
        => _repo.GetRecentAsync(request.Take, ct);
}
