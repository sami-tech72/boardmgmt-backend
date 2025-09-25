namespace BoardMgmt.Application.Chat;

public record ListConversationsQuery(string ForUserId)
  : MediatR.IRequest<IReadOnlyList<ConversationListItemDto>>;

public record GetConversationQuery(Guid ConversationId, string ForUserId)
  : MediatR.IRequest<ConversationDetailDto>;

public record GetHistoryQuery(Guid ConversationId, DateTime? BeforeUtc, int Take, string ForUserId, Guid? ThreadRootId = null)
  : MediatR.IRequest<PagedResult<ChatMessageDto>>;

public record SearchMessagesQuery(string ForUserId, string Term, int Take = 50)
  : MediatR.IRequest<IReadOnlyList<ChatMessageDto>>;

public record GetThreadConversationIdQuery(Guid ThreadRootMessageId) : MediatR.IRequest<Guid>;
