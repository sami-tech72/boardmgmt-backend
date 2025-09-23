using MediatR;
using BoardMgmt.Application.Messages.DTOs;

namespace BoardMgmt.Application.Messages.Commands;

public record CreateMessageCommand(
    Guid SenderId,
    string Subject,
    string Body,
    string Priority,                 // "Low|Normal|High"
    bool ReadReceiptRequested,
    bool IsConfidential,
    IReadOnlyList<Guid> RecipientIds,
    bool AsDraft = true              // if false => mark Sent immediately
) : IRequest<MessageDto>;
