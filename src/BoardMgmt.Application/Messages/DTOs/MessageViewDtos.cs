namespace BoardMgmt.Application.Messages.DTOs;

public record MinimalUserDto(Guid Id, string? FullName, string? Email);

public record MessageListItemVm(
    Guid Id,
    string Subject,
    string Preview,                 // short body preview for list
    MinimalUserDto? FromUser,       // sender (nullable if missing)
    string Priority,                // "Low|Normal|High"
    string Status,                  // "Draft|Sent"
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
