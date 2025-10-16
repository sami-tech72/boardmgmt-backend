namespace BoardMgmt.Application.Common.Interfaces;

public interface IGraphSubscriptionManager
{
    Task<GraphSubscriptionDescriptor> CreateTeamsTranscriptSubscriptionAsync(
        DateTimeOffset? startFromUtc,
        TimeSpan? lifetime,
        CancellationToken ct = default);
}

public sealed record GraphSubscriptionDescriptor(
    string Id,
    string Resource,
    string ChangeType,
    DateTimeOffset? ExpirationDateTime,
    string? ClientState);
