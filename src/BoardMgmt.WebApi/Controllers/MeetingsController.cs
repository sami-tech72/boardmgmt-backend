using BoardMgmt.Application.Meetings.Commands;
using BoardMgmt.Application.Meetings.Queries;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Infrastructure.Persistence;
using BoardMgmt.WebApi.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // ⬅️ needed for ToListAsync

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeetingsController : ControllerBase
{
    private readonly IMediator _mediator;
    public MeetingsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
        => this.OkApi(await _mediator.Send(new GetMeetingsQuery()));

    public record CreateMeetingDto(
    string Title,
    string? Description,
    MeetingType? Type,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? EndAt,
    string Location,
    List<string>? AttendeeUserIds  // from UI
);

    [HttpPost]
    [Authorize(Roles = "Admin,Secretary")]
    public async Task<IActionResult> Create([FromBody] CreateMeetingDto dto)
    {
        var id = await _mediator.Send(new CreateMeetingCommand(
            dto.Title, dto.Description, dto.Type,
            dto.ScheduledAt, dto.EndAt, dto.Location,
           attendeeUserIds: dto.AttendeeUserIds   // <<— pass through
        ));
        return this.CreatedApi(nameof(GetAll), new { id }, new { id }, "Meeting created");
    }

    // ✅ Minimal list for your upload modal dropdown
    [HttpGet("minimal")]
    [Authorize] // change to [AllowAnonymous] if you want
    public async Task<ActionResult<IReadOnlyList<object>>> Minimal([FromServices] AppDbContext db)
    {
        var items = await db.Meetings
            .OrderByDescending(m => m.ScheduledAt)
            .Select(m => new { id = m.Id, title = m.Title, scheduledAt = m.ScheduledAt })
            .ToListAsync();

        return Ok(items);
    }



    [HttpGet("{id:guid}/eligible-voters")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<object>>> EligibleVoters(
    Guid id, [FromServices] AppDbContext db)
    {
        var meeting = await db.Meetings
            .Include(m => m.Attendees)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (meeting is null) return NotFound();

        // If you added MeetingAttendee.UserId, include it here
        var list = meeting.Attendees
            .Select(a => new { a.Id, a.Name, a.Role, a.UserId })
            .ToList();

        return Ok(list);
    }
}
