using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Dashboard.DTOs;
using MediatR;

namespace BoardMgmt.Application.Dashboard.Queries;

public sealed record GetStatsDetailQuery(
    string Kind,   // "meetings" | "documents" | "votes" | "messages"
    int Page,
    int PageSize,
    Guid? UserId   // nullable if not scoping messages to a user
) : IRequest<object>; // returns a PagedResultDto<T> boxed as object

public sealed class GetStatsDetailQueryHandler : IRequestHandler<GetStatsDetailQuery, object>
{
    private readonly IMeetingReadRepository _meetings;
    private readonly IDocumentReadRepository _docs;
    private readonly IVoteReadRepository _votes;
    private readonly IUserReadRepository _users;

    public GetStatsDetailQueryHandler(
        IMeetingReadRepository meetings,
        IDocumentReadRepository docs,
        IVoteReadRepository votes,
        IUserReadRepository users)
    {
        _meetings = meetings;
        _docs = docs;
        _votes = votes;
        _users = users;
    }

    public async Task<object> Handle(GetStatsDetailQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        switch (request.Kind)
        {
            case "meetings":
                {
                    var (total, items) = await _meetings.GetUpcomingPagedAsync(page, pageSize, ct);
                    return new PagedResultDto<MeetingItemDto>(total, page, pageSize, items);
                }
            case "documents":
                {
                    var (total, items) = await _docs.GetActivePagedAsync(page, pageSize, ct);
                    return new PagedResultDto<DocumentItemDto>(total, page, pageSize, items);
                }
            case "votes":
                {
                    var (total, items) = await _votes.GetPendingPagedAsync(page, pageSize, ct);
                    return new PagedResultDto<VoteItemDto>(total, page, pageSize, items);
                }
            case "users":
                {
                    var (total, items) = await _users.GetActivePagedAsync(page, pageSize, ct);
                    return new PagedResultDto<ActiveUserItemDto>(total, page, pageSize, items);
                }

        }

        // default empty
        return new PagedResultDto<object>(0, page, pageSize, Array.Empty<object>());
    }
}
