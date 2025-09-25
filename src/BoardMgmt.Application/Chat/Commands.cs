namespace BoardMgmt.Application.Chat;


public record CreateDirectConversationCommand(string CreatorId, IReadOnlyList<string> MemberIds)
  : MediatR.IRequest<Guid>;
public record CreateChannelCommand(string CreatorId, string Name, bool IsPrivate, IReadOnlyList<string> MemberIds)
  : MediatR.IRequest<Guid>;
public record JoinChannelCommand(Guid ConversationId, string UserId) : MediatR.IRequest<bool>;
public record LeaveConversationCommand(Guid ConversationId, string UserId) : MediatR.IRequest<bool>;

public record SendChatMessageCommand(Guid ConversationId, string SenderId, string BodyHtml, Guid? ThreadRootId)
  : MediatR.IRequest<Guid>;
public record EditChatMessageCommand(Guid MessageId, string EditorId, string BodyHtml) : MediatR.IRequest<bool>;
public record DeleteChatMessageCommand(Guid MessageId, string RequestorId) : MediatR.IRequest<bool>;

public record AddReactionCommand(Guid MessageId, string UserId, string Emoji) : MediatR.IRequest<bool>;
public record RemoveReactionCommand(Guid MessageId, string UserId, string Emoji) : MediatR.IRequest<bool>;

public record AddChatAttachmentsCommand(
    Guid MessageId,
    IReadOnlyList<(string FileName, string ContentType, long FileSize, string StoragePath)> Files
) : MediatR.IRequest<int>;

public record MarkConversationReadCommand(Guid ConversationId, string UserId, DateTime ReadAtUtc)
  : MediatR.IRequest<bool>;

public record SetTypingCommand(Guid ConversationId, string UserId, bool IsTyping) : MediatR.IRequest;

public record CreateOrGetDirectConversationCommand(
    string UserId,
    string OtherUserId
) : MediatR.IRequest<Guid>;


