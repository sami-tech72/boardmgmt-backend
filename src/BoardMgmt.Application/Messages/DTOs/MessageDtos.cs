namespace BoardMgmt.Application.Messages.DTOs;

public record MinimalUserDto(Guid Id, string? FullName, string? Email);

public record MessageAttachmentDto(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    long FileSize
);

public record MessageRecipientDto(Guid UserId, bool IsRead, DateTime? ReadAtUtc);

public record MessageDto(
    Guid Id,
    Guid SenderId,
    string Subject,
    string Body,
    string Priority,
    bool ReadReceiptRequested,
    bool IsConfidential,
    string Status,
    DateTime? SentAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<MessageRecipientDto> Recipients,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    bool HasAttachments
);

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

public record PagedResult<T>(IReadOnlyList<T> Items, int Total);
