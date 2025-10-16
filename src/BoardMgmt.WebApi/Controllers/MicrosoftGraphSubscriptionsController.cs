using BoardMgmt.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("integrations/msgraph/subscriptions")]
public sealed class MicrosoftGraphSubscriptionsController : ControllerBase
{
    private readonly IGraphSubscriptionManager _subscriptions;
    private readonly ILogger<MicrosoftGraphSubscriptionsController> _logger;

    public MicrosoftGraphSubscriptionsController(
        IGraphSubscriptionManager subscriptions,
        ILogger<MicrosoftGraphSubscriptionsController> logger)
    {
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public sealed class CreateTeamsTranscriptSubscriptionRequest
    {
        /// <summary>
        /// Optional UTC timestamp used to filter the onlineMeetings resource. Defaults to 12 hours ago.
        /// </summary>
        public DateTimeOffset? StartFromUtc { get; init; }

        /// <summary>
        /// Requested subscription lifetime in minutes. Defaults to 55 minutes and is clamped to Graph limits.
        /// </summary>
        public int? LifetimeMinutes { get; init; }
    }

    [HttpPost("teams-transcripts")]
    [Authorize(Policy = "Meetings.Update")]
    public async Task<IActionResult> CreateTeamsTranscriptSubscription(
        [FromBody] CreateTeamsTranscriptSubscriptionRequest? request,
        CancellationToken ct)
    {
        try
        {
            TimeSpan? lifetime = null;
            if (request?.LifetimeMinutes is int minutes)
            {
                if (minutes <= 0)
                    return BadRequest(new { error = "LifetimeMinutes must be greater than zero." });

                lifetime = TimeSpan.FromMinutes(minutes);
            }

            var descriptor = await _subscriptions.CreateTeamsTranscriptSubscriptionAsync(
                request?.StartFromUtc,
                lifetime,
                ct);

            return Ok(new
            {
                descriptor.Id,
                descriptor.Resource,
                descriptor.ChangeType,
                descriptor.ExpirationDateTime,
                descriptor.ClientState
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to create Microsoft Graph subscription.");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request while creating Microsoft Graph subscription.");
            return BadRequest(new { error = ex.Message });
        }
    }
}
