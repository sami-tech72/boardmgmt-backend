using MediatR;
using BoardMgmt.Application.Messages.DTOs;

namespace BoardMgmt.Application.Messages.Queries;

public record GetMessageViewQuery(Guid Id) : IRequest<MessageDetailVm?>;
public record GetMessageThreadQuery(Guid AnchorMessageId, Guid CurrentUserId) : IRequest<MessageThreadVm>;
public record ListMessageItemsQuery(
    Guid? ForUserId,
    Guid? SentByUserId,
    string? Q,
    string? Priority,
    string? Status,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<MessageListItemVm>>;
