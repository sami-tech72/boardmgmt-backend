namespace BoardMgmt.Application.Chat;

public record MinimalUserDto(string Id, string? FullName, string? Email);

public record ConversationListItemDto(
    Guid Id, string Name, string Type, bool IsPrivate,
    int UnreadCount, DateTime? LastMessageAtUtc
);

public record ConversationDetailDto(
    Guid Id, string Name, string Type, bool IsPrivate,
    IReadOnlyList<MinimalUserDto> Members
);

public record ReactionDto(string Emoji, int Count, bool ReactedByMe);

public record ChatAttachmentDto(Guid AttachmentId, string FileName, string ContentType, long FileSize);

public record ChatMessageDto(
    Guid Id, Guid ConversationId, Guid? ThreadRootId,
    MinimalUserDto FromUser, string BodyHtml,
    DateTime CreatedAtUtc, DateTime? EditedAtUtc, bool IsDeleted,
    IReadOnlyList<ChatAttachmentDto> Attachments,
    IReadOnlyList<ReactionDto> Reactions,
    int ThreadReplyCount
);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total);
