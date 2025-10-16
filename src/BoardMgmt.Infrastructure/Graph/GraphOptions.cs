// Backend/src/BoardMgmt.Infrastructure/Graph/GraphOptions.cs
namespace BoardMgmt.Infrastructure.Graph;
public sealed class GraphOptions
{
    public string TenantId { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string MailboxAddress { get; set; } = default!;
    public string? WebhookNotificationUrl { get; set; }
    public string? WebhookClientState { get; set; }
}
