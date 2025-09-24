using MediatR;
using BoardMgmt.Application.Messages.DTOs;

namespace BoardMgmt.Application.Messages.Commands;

public record CreateMessageCommand(
    Guid SenderId,
    string Subject,
    string Body,
    string Priority,
    bool ReadReceiptRequested,
    bool IsConfidential,
    IReadOnlyList<Guid> RecipientIds,
    bool AsDraft
) : IRequest<Guid>;

public record UpdateMessageCommand(
    Guid MessageId,
    string Subject,
    string Body,
    string Priority,
    bool ReadReceiptRequested,
    bool IsConfidential,
    IReadOnlyList<Guid> RecipientIds
) : IRequest<bool>;

public record SendMessageCommand(Guid MessageId) : IRequest<bool>;
public record DeleteMessageCommand(Guid MessageId) : IRequest<bool>;
public record MarkMessageReadCommand(Guid MessageId, Guid UserId) : IRequest<bool>;

public record AddMessageAttachmentsCommand(
    Guid MessageId,
    IReadOnlyList<(string FileName, string ContentType, long FileSize, string StoragePath)> Files
) : IRequest<int>;
