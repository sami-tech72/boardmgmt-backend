// Hubs/ChatHubEvents.cs
using BoardMgmt.Application.Chat;
using Microsoft.AspNetCore.SignalR;

namespace BoardMgmt.WebApi.Hubs
{
    public static class ChatHubEvents
    {
        public record MessageChangePayload(Guid ConversationId, Guid? ThreadRootId, ChatMessageDto Message, ChatMessageDto? ThreadRoot);

        public static Task MessageCreated(IHubContext<ChatHub> hub, MessageChangePayload payload)
            => hub.Clients.Group($"conv:{payload.ConversationId}")
                .SendAsync("MessageCreated", payload);

        public static Task MessageEdited(IHubContext<ChatHub> hub, MessageChangePayload payload)
            => hub.Clients.Group($"conv:{payload.ConversationId}")
                .SendAsync("MessageEdited", payload);

        public static Task MessageDeleted(IHubContext<ChatHub> hub, MessageChangePayload payload)
            => hub.Clients.Group($"conv:{payload.ConversationId}")
                .SendAsync("MessageDeleted", payload);

        public static Task ReactionUpdated(IHubContext<ChatHub> hub, Guid conversationId, object payload)
            => hub.Clients.Group($"conv:{conversationId}")
                .SendAsync("ReactionUpdated", payload);

        public static Task Typing(IHubContext<ChatHub> hub, Guid conversationId, string userId, bool isTyping)
            => hub.Clients.Group($"conv:{conversationId}")
                .SendAsync("Typing", new { conversationId, userId, isTyping, at = DateTime.UtcNow });
    }
}
