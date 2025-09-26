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
    private readonly IMessageReadRepository _messages;

    public GetStatsDetailQueryHandler(
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
            case "messages":
                {
                    var (total, items) = await _messages.GetUnreadPagedAsync(request.UserId, page, pageSize, ct);
                    return new PagedResultDto<UnreadMessageItemDto>(total, page, pageSize, items);
                }
        }

        // default empty
        return new PagedResultDto<object>(0, page, pageSize, Array.Empty<object>());
    }
}
