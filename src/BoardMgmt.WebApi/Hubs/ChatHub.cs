// Hubs/ChatHub.cs
using BoardMgmt.WebApi.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BoardMgmt.WebApi.Hubs;

[Authorize(Policy = PolicyNames.Messages.View)]
public class ChatHub : Hub
{
    public Task JoinUser(string userId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

    public Task JoinConversation(Guid conversationId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"conv:{conversationId}");

    public Task LeaveConversation(Guid conversationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv:{conversationId}");
}
