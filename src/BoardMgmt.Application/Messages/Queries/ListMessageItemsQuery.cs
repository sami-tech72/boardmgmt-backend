using MediatR;
using BoardMgmt.Application.Messages.DTOs;

namespace BoardMgmt.Application.Messages.Queries;

public record ListMessageItemsQuery(
    Guid? ForUserId,
    Guid? SentByUserId,
    string? Q,
    string? Priority,
    string? Status,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<MessageListItemVm>>;
