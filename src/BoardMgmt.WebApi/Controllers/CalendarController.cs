using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Calendars.Commands;
using BoardMgmt.Application.Calendars.Queries;
using BoardMgmt.Domain.Calendars;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CalendarController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICalendarServiceSelector _selector; // keep if you also call external providers

    public CalendarController(IMediator mediator, ICalendarServiceSelector selector)
    {
        _mediator = mediator;
        _selector = selector;
    }

    // GET /api/calendar/range?start=2025-09-01T00:00:00Z&end=2025-09-30T23:59:59Z&provider=Zoom|Microsoft365|All
    [HttpGet("range")]
    [Authorize(Policy = "Meetings.View")]
    public async Task<ActionResult<IReadOnlyList<CalendarEventDto>>> GetRange(
        [FromQuery] DateTimeOffset? start,
        [FromQuery] DateTimeOffset? end,
        [FromQuery] string? provider = "All",
        CancellationToken ct = default)
    {
        var s = start ?? DateTimeOffset.UtcNow.AddDays(-7);
        var e = end ?? DateTimeOffset.UtcNow.AddDays(30);

        var list = new List<CalendarEventDto>();
        var warnings = new List<string>();

        async Task Add(string key)
        {
            try
            {
                var svc = _selector.For(key);
                var part = await svc.ListRangeAsync(s, e, ct);
                list.AddRange(part);
            }
            catch (Exception ex)
            {
                // Don’t block the other provider; return a warning header
                warnings.Add($"{key}: {ex.Message}");
            }
        }

        var normalizedProvider = CalendarProviders.Normalize(provider);

        if (normalizedProvider == CalendarProviders.Zoom)
        {
            await Add(CalendarProviders.Zoom);
        }
        else if (normalizedProvider == CalendarProviders.Microsoft365)
        {
            await Add(CalendarProviders.Microsoft365);
        }
        else // All
        {
            await Task.WhenAll(Add(CalendarProviders.Microsoft365), Add(CalendarProviders.Zoom));
        }

        if (warnings.Count > 0)
            Response.Headers.Append("X-Warnings", string.Join(" | ", warnings));

        return Ok(list.OrderBy(x => x.StartUtc).ToList());
    }

    [HttpGet("range-db")]
    [Authorize(Policy = "Meetings.View")]
    public async Task<IReadOnlyList<CalendarEventDto>> GetRangeFromDb(
        [FromQuery] DateTimeOffset? start,
        [FromQuery] DateTimeOffset? end,
        CancellationToken ct = default)
    {
        var s = start ?? DateTimeOffset.UtcNow.AddDays(-7);
        var e = end ?? DateTimeOffset.UtcNow.AddDays(30);

        // Guard: if somehow start >= end, swap or expand
        if (s >= e) e = s.AddDays(1);

        return await _mediator.Send(new GetCalendarRangeFromDbQuery(s, e), ct);
    }



    [HttpPut("move/{id:guid}")]
    [Authorize(Policy = "Meetings.Update")]
    public async Task<IActionResult> Move(Guid id, [FromBody] MoveCalendarEventDto body, CancellationToken ct)
    {
        if (body is null) return BadRequest("Body required.");

        if (body.EndUtc.HasValue && body.EndUtc.Value <= body.StartUtc)
            return BadRequest("EndUtc must be after StartUtc.");

        var ok = await _mediator.Send(new MoveCalendarEventCommand(
            id,
            body.StartUtc,
            body.EndUtc
        ), ct);

        return ok ? NoContent() : NotFound();
    }

    public sealed record MoveCalendarEventDto(DateTimeOffset StartUtc, DateTimeOffset? EndUtc);
}
