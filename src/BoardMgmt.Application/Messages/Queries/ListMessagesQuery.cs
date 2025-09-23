using MediatR;
using BoardMgmt.Application.Messages.DTOs;

namespace BoardMgmt.Application.Messages.Queries;

public record ListMessagesQuery(
    Guid? ForUserId,               // inbox for user (recipient), or null = all
    Guid? SentByUserId,            // outbox for sender
    string? Q,                     // search subject/body
    string? Priority,              // Low|Normal|High
    string? Status,                // Draft|Sent
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<MessageDto>>;
