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
    // GET /api/meetings
    // -----------------------------
    [HttpGet]
    [Authorize(Policy = "Meetings.View")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => this.OkApi(await _mediator.Send(new GetMeetingsQuery(), ct));

    // -----------------------------
    // GET /api/meetings/{id}
    // -----------------------------
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Meetings.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetMeetingByIdQuery(id), ct);
        return dto is null
            ? this.NotFoundApi("not_found", "Meeting not found.", new { id })
            : this.OkApi(dto, "Meeting loaded");
    }

    // -----------------------------
    // POST /api/meetings
    // -----------------------------
    public record CreateMeetingDto(
        string Title,
        string? Description,
        MeetingType? Type,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? EndAt,
        string Location,
        List<string>? AttendeeUserIds,
        List<string>? Attendees = null,
        string Provider = "Zoom",           // default to Zoom
        string? HostIdentity = null         // mailbox (M365) or host email (Zoom)
    );

    [HttpPost]
    [Authorize(Policy = "Meetings.Create")]
    public async Task<IActionResult> Create([FromBody] CreateMeetingDto dto, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateMeetingCommand(
            dto.Title, dto.Description, dto.Type, dto.ScheduledAt, dto.EndAt, dto.Location,
            dto.AttendeeUserIds, dto.Attendees, dto.Provider, dto.HostIdentity
        ), ct);

        // Location header -> GET /api/meetings/{id}
        return this.CreatedApi(nameof(GetById), new { id }, new { id }, "Meeting created");
    }

    // -----------------------------
    // PUT /api/meetings/{id}
    // -----------------------------
    // Remove duplicate Attendees; use AttendeesRich
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
    [Authorize(Policy = "Meetings.Update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMeetingDto dto, CancellationToken ct)
    {
        if (id != dto.Id)
            return this.BadRequestApi("id_mismatch", "Route id and body id must match.", new { routeId = id, bodyId = dto.Id });

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
        ), ct);

        return ok
            ? this.OkApi(new { id }, "Meeting updated")
            : this.BadRequestApi("update_failed", "Could not update meeting.", new { id });
    }

    // -----------------------------
    // GET /api/meetings/select-list (lightweight)
    // -----------------------------
    [AllowAnonymous]
    [HttpGet("select-list")]
    public async Task<IActionResult> SelectList(CancellationToken ct)
    {
        var list = await _mediator.Send(new GetMeetingsSelectListQuery(), ct);
        return this.OkApi(list);
    }

    // -----------------------------
    // DELETE /api/meetings/{id}
    // -----------------------------
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Meetings.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new DeleteMeetingCommand(id), ct);
        return ok
            ? this.NoContentApi() // uniform 204 with envelope
            : this.NotFoundApi("not_found", "Meeting not found.", new { id });
    }

    // -----------------------------
    // POST /api/meetings/{id}/transcripts/ingest
    // -----------------------------
    [HttpPost("{id:guid}/transcripts/ingest")]
    [Authorize(Policy = "Meetings.Update")] // or "Meetings.IngestTranscript"
    public async Task<IActionResult> IngestTranscript(Guid id, CancellationToken ct)
    {
        var count = await _mediator.Send(new IngestTranscriptCommand(id), ct);
        return this.OkApi(new { meetingId = id, utterances = count }, "Transcript ingested");
    }

    // -----------------------------
    // GET /api/meetings/{id}/transcripts
    // returns latest transcript by CreatedUtc
    // -----------------------------
    [HttpGet("{id:guid}/transcripts")]
    [Authorize(Policy = "Meetings.View")]
    public async Task<IActionResult> GetTranscript(Guid id, CancellationToken ct)
    {
        var tr = await _mediator.Send(new GetTranscriptByMeetingIdQuery(id), ct);
        return tr is null
            ? this.NotFoundApi("not_found", "Transcript not found.", new { meetingId = id })
            : this.OkApi(tr);
    }
}
