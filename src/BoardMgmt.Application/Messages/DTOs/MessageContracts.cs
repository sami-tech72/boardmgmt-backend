namespace BoardMgmt.Application.Messages.DTOs;

public record MinimalUserDto(Guid Id, string? FullName, string? Email);
public record MessageAttachmentDto(Guid AttachmentId, string FileName, string ContentType, long FileSize);
public record MessageRecipientDto(Guid UserId, bool IsRead, DateTime? ReadAtUtc);

public record MessageListItemVm(
    Guid Id,
    string Subject,
    string Preview,
    MinimalUserDto? FromUser,
    string Priority,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    DateTime UpdatedAtUtc,
    bool HasAttachments
);

public record MessageDetailVm(
    Guid Id,
    string Subject,
    string Body,
    MinimalUserDto? FromUser,
    IReadOnlyList<MinimalUserDto> Recipients,
    string Priority,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<MessageAttachmentDto> Attachments
);

public record MessageBubbleVm(
    Guid Id,
    MinimalUserDto FromUser,
    string Body,
    DateTime CreatedAtUtc,
    IReadOnlyList<MessageAttachmentDto> Attachments
);

public record MessageThreadVm(
    Guid AnchorMessageId,
    string Subject,
    IReadOnlyList<MinimalUserDto> Participants,
    IReadOnlyList<MessageBubbleVm> Items
);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total);
