// ------------------------------
// Application/Dashboard/Queries/GetDashboardStatsQuery.cs
// ------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Dashboard.DTOs;
using MediatR;

namespace BoardMgmt.Application.Dashboard.Queries;

public sealed record GetDashboardStatsQuery(Guid? UserId) : IRequest<DashboardStatsDto>;

public sealed class GetDashboardStatsQueryHandler
    : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly IMeetingReadRepository _meetings;
    private readonly IDocumentReadRepository _docs;
    private readonly IVoteReadRepository _votes;
    private readonly IMessageReadRepository _messages;

    public GetDashboardStatsQueryHandler(
        IMeetingReadRepository meetings,
        IDocumentReadRepository docs,
        IVoteReadRepository votes,
        IMessageReadRepository messages)
    {
        _meetings = meetings;
        _docs = docs;
        _votes = votes;
        _messages = messages;
    }

    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Sequential awaits to avoid concurrent DbContext usage
        var upcoming = await _meetings.CountUpcomingAsync(now, ct);
        var activeDocs = await _docs.CountActiveAsync(ct);
        var pendingVotes = await _votes.CountPendingAsync(ct);
        var unread = await _messages.CountUnreadAsync(request.UserId, ct);

        return new DashboardStatsDto(
            upcoming,
            activeDocs,
            pendingVotes,
            unread
        );
    }
}
