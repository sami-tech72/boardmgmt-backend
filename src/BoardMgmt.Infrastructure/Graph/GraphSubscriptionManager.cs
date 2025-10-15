using System.Globalization;
using BoardMgmt.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace BoardMgmt.Infrastructure.Graph;

public sealed class GraphSubscriptionManager(
    GraphServiceClient graph,
    IOptions<GraphOptions> options,
    ILogger<GraphSubscriptionManager> logger) : IGraphSubscriptionManager
{
    private readonly GraphServiceClient _graph = graph;
    private readonly GraphOptions _options = options.Value;
    private readonly ILogger<GraphSubscriptionManager> _logger = logger;

    private static readonly TimeSpan MinLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(23); // Graph currently allows up to 1 day.

    public async Task<GraphSubscriptionDescriptor> CreateTeamsTranscriptSubscriptionAsync(
        DateTimeOffset? startFromUtc,
        TimeSpan? lifetime,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookNotificationUrl))
            throw new InvalidOperationException("Graph:WebhookNotificationUrl is not configured.");

        if (!Uri.TryCreate(_options.WebhookNotificationUrl, UriKind.Absolute, out var notificationUri)
            || notificationUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Graph webhook notification URL must be an absolute HTTPS URL.");
        }

        var resolvedLifetime = lifetime ?? TimeSpan.FromMinutes(55);
        if (resolvedLifetime < MinLifetime)
            resolvedLifetime = MinLifetime;
        if (resolvedLifetime > MaxLifetime)
            resolvedLifetime = MaxLifetime;

        var start = (startFromUtc ?? DateTimeOffset.UtcNow.AddHours(-12)).ToUniversalTime();
        var filterValue = start.ToString("o", CultureInfo.InvariantCulture);
        var resource = $"/communications/onlineMeetings?$filter=StartDateTime ge {filterValue}";

        var changeType = "updated"; // transcripts surface as updates on the meeting resource

        var subscription = new Subscription
        {
            ChangeType = changeType,
            NotificationUrl = notificationUri.ToString(),
            Resource = resource,
            ClientState = _options.WebhookClientState,
            ExpirationDateTime = DateTimeOffset.UtcNow.Add(resolvedLifetime),
            LatestSupportedTlsVersion = "v1_2"
        };

        _logger.LogInformation(
            "Creating Microsoft Graph subscription for resource {Resource} with expiration {Expiration}.",
            subscription.Resource,
            subscription.ExpirationDateTime);

        var created = await _graph.Subscriptions.PostAsync(subscription, cancellationToken: ct)
            ?? throw new InvalidOperationException("Microsoft Graph did not return subscription details.");

        if (string.IsNullOrWhiteSpace(created.Id))
            throw new InvalidOperationException("Microsoft Graph returned a subscription without an identifier.");

        _logger.LogInformation(
            "Created Microsoft Graph subscription {SubscriptionId} with expiration {Expiration}.",
            created.Id,
            created.ExpirationDateTime);

        return new GraphSubscriptionDescriptor(
            created.Id!,
            created.Resource ?? resource,
            created.ChangeType ?? changeType,
            created.ExpirationDateTime,
            created.ClientState);
    }
}
