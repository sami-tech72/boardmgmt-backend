namespace BoardMgmt.Application.Messages.DTOs;

public record MessageAttachmentDto(Guid Id, string FileName, string ContentType, long FileSize);
public record MessageRecipientDto(Guid UserId, bool IsRead, DateTime? ReadAtUtc);

public record MessageDto(
    Guid Id,
    Guid SenderId,
    string Subject,
    string Body,
    string Priority,         // "Low|Normal|High"
    bool ReadReceiptRequested,
    bool IsConfidential,
    string Status,           // "Draft|Sent"
    DateTime? SentAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<MessageRecipientDto> Recipients,
    IReadOnlyList<MessageAttachmentDto> Attachments
);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total);
