// Backend/src/BoardMgmt.Api/Controllers/CalendarController.cs
using Microsoft.AspNetCore.Mvc;
using BoardMgmt.Application.Calendars;
using BoardMgmt.Domain.Entities;

[ApiController]
[Route("api/calendar")]
public sealed class CalendarController : ControllerBase
{
    private readonly ICalendarService _svc;
    public CalendarController(ICalendarService svc) => _svc = svc;

    // POST /api/calendar/events
    [HttpPost("events")]
    public async Task<IActionResult> Create([FromBody] CreateMeetingRequest req, CancellationToken ct)
    {
        var meeting = new Meeting
        {
            Title = req.Title,
            Description = req.Description,
            ScheduledAt = req.StartsAtUtc,                 // UTC in request
            EndAt = req.EndsAtUtc,
            Location = string.IsNullOrWhiteSpace(req.Location) ? "TBD" : req.Location,
            Attendees = req.Attendees.Select(a => new MeetingAttendee
            {
                Name = a.Name ?? a.Email,
                Email = a.Email,
                IsRequired = a.IsRequired
            }).ToList()
        };

        var eventId = await _svc.CreateEventAsync(meeting, ct);
        return Ok(new { id = eventId });
    }

    // GET /api/calendar/upcoming?take=10
    [HttpGet("upcoming")]
    public async Task<IActionResult> Upcoming([FromQuery] int take = 10, CancellationToken ct = default)
        => Ok(await _svc.ListUpcomingAsync(take, ct));

    // DELETE /api/calendar/events/{id}
    [HttpDelete("events/{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _svc.CancelEventAsync(id, ct);
        return NoContent();
    }



}

public sealed record CreateMeetingRequest(
    string Title,
    string? Description,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string? Location,
    List<CreateMeetingAttendee> Attendees);

public sealed record CreateMeetingAttendee(string Email, string? Name, bool IsRequired);
