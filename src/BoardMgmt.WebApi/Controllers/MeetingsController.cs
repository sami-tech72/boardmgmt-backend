using BoardMgmt.Application.Meetings.Commands;
using BoardMgmt.Application.Meetings.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        => Ok(await _mediator.Send(new GetMeetingsQuery()));

    public record CreateMeetingDto(
        string Title,
        string? Description,
        string? Type,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? EndAt,
        string Location,
        List<string>? Attendees
    );

    [HttpPost]
    [Authorize(Roles = "Admin,Secretary")]
    public async Task<IActionResult> Create([FromBody] CreateMeetingDto dto)
    {
        var id = await _mediator.Send(new CreateMeetingCommand(
            dto.Title, dto.Description, dto.Type, dto.ScheduledAt, dto.EndAt, dto.Location, dto.Attendees
        ));
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }
}
