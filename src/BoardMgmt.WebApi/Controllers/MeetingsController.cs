using BoardMgmt.Application.Meetings.Commands;
using BoardMgmt.Application.Meetings.DTOs;
using BoardMgmt.Application.Meetings.Queries;
using BoardMgmt.Domain.Entities;
using BoardMgmt.WebApi.Common.Http;
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

    // -----------------------------
    // GET all meetings
    // -----------------------------
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
        => this.OkApi(await _mediator.Send(new GetMeetingsQuery()));

    // -----------------------------
    // GET /api/meetings/{id}
    // -----------------------------
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var dto = await _mediator.Send(new GetMeetingByIdQuery(id));
        return dto is null
            ? NotFound()
            : this.OkApi(dto, "Meeting loaded");
    }

    // -----------------------------
    // Create DTO (POST body)
    // -----------------------------
    public record CreateMeetingDto(
        string Title,
        string? Description,
        MeetingType? Type,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? EndAt,
        string Location,
        List<string>? AttendeeUserIds,
        List<string>? Attendees = null
    );

    // -----------------------------
    // POST /api/meetings
    // -----------------------------
    [HttpPost]
    [Authorize(Roles = "Admin,Secretary")]
    public async Task<IActionResult> Create([FromBody] CreateMeetingDto dto)
    {
        var id = await _mediator.Send(new CreateMeetingCommand(
            dto.Title,
            dto.Description,
            dto.Type,
            dto.ScheduledAt,
            dto.EndAt,
            dto.Location,
            attendeeUserIds: dto.AttendeeUserIds,
            Attendees: dto.Attendees
        ));

        return this.CreatedApi(nameof(GetById), new { id }, new { id }, "Meeting created");
    }

    // -----------------------------
    // Update DTO (PUT body)
    // -----------------------------

    // WebApi controller (can be in DTOs namespace if you prefer)
    // ✅ Update DTO: remove duplicate Attendees; add AttendeesRich
    // BoardMgmt.WebApi/Controllers/MeetingsController.cs  (excerpt)
    public record UpdateMeetingDto(
        Guid Id,
        string Title,
        string? Description,
        MeetingType? Type,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? EndAt,
        string Location,
        List<string>? AttendeeUserIds,
        List<BoardMgmt.Application.Meetings.Commands.UpdateAttendeeDto>? AttendeesRich
    );

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Secretary")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMeetingDto dto)
    {
        if (id != dto.Id)
            return this.BadRequestApi("id_mismatch", "Route id and body id must match.");

        var ok = await _mediator.Send(new UpdateMeetingCommand(
            dto.Id,
            dto.Title,
            dto.Description,
            dto.Type,
            dto.ScheduledAt,
            dto.EndAt,
            dto.Location,
            dto.AttendeeUserIds,
            dto.AttendeesRich
        ));

        return ok
            ? this.OkApi(new { id }, "Meeting updated")
            : this.BadRequestApi("update_failed", "Could not update meeting.");
    }

}
