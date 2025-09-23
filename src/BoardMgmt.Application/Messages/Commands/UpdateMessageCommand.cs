using MediatR;
using BoardMgmt.Application.Messages.DTOs;

namespace BoardMgmt.Application.Messages.Commands;

public record UpdateMessageCommand(
    Guid MessageId,
    string Subject,
    string Body,
    string Priority,
    bool ReadReceiptRequested,
    bool IsConfidential,
    IReadOnlyList<Guid> RecipientIds
) : IRequest<MessageDto>;
