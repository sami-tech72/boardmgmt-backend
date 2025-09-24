using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BoardMgmt.WebApi.Hubs;

[Authorize]
public class MessagesHub : Hub
{
    public Task JoinUser(string userId) => Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
}
