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


    public record CreateMeetingDto(string Title, DateTimeOffset ScheduledAt, string Location);


    [HttpPost]
    [Authorize(Roles = "Admin,Secretary")]
    public async Task<IActionResult> Create([FromBody] CreateMeetingDto dto)
    {
        var id = await _mediator.Send(new CreateMeetingCommand(dto.Title, dto.ScheduledAt, dto.Location));
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }
}