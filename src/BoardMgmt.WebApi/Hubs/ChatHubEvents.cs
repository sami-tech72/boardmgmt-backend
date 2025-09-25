using Microsoft.AspNetCore.SignalR;

namespace BoardMgmt.WebApi.Hubs
{
    public static class ChatHubEvents
    {
        public static Task MessageCreated(IHubContext<ChatHub> hub, Guid conversationId, object payload)
            => hub.Clients.Group($"conv:{conversationId}").SendAsync("MessageCreated", payload);

        public static Task MessageEdited(IHubContext<ChatHub> hub, Guid conversationId, object payload)
            => hub.Clients.Group($"conv:{conversationId}").SendAsync("MessageEdited", payload);

        public static Task MessageDeleted(IHubContext<ChatHub> hub, Guid conversationId, object payload)
            => hub.Clients.Group($"conv:{conversationId}").SendAsync("MessageDeleted", payload);

        public static Task ReactionUpdated(IHubContext<ChatHub> hub, Guid conversationId, object payload)
            => hub.Clients.Group($"conv:{conversationId}").SendAsync("ReactionUpdated", payload);

        public static Task Typing(IHubContext<ChatHub> hub, Guid conversationId, Guid userId, bool isTyping)
            => hub.Clients.Group($"conv:{conversationId}")
                         .SendAsync("Typing", new { conversationId, userId, isTyping, at = DateTime.UtcNow });
    }
}
