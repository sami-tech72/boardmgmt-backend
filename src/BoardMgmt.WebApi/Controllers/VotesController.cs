using BoardMgmt.Application.Votes.Commands;
using BoardMgmt.Application.Votes.DTOs;
using BoardMgmt.Application.Votes.Queries;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VotesController : ControllerBase
{
    private readonly IMediator _mediator;
    public VotesController(IMediator mediator) => _mediator = mediator;

    // Create a vote (from your modal)
    public sealed record CreateVoteDto(
        string Title,
        string? Description,
        VoteType Type,
        bool AllowAbstain,
        bool Anonymous,
        DateTimeOffset Deadline,
        VoteEligibility Eligibility,
        Guid? MeetingId,
        Guid? AgendaItemId,
        List<string>? Options,          // MultipleChoice only
        List<string>? SpecificUserIds   // Eligibility=SpecificUsers
    );

    [HttpPost]
    [Authorize(Roles = "Admin,Secretary,BoardMember")] // adjust as needed
    public async Task<ActionResult<object>> Create([FromBody] CreateVoteDto dto)
    {
        var id = await _mediator.Send(new CreateVoteCommand(
            dto.Title, dto.Description, dto.Type, dto.AllowAbstain, dto.Anonymous,
            dto.Deadline, dto.Eligibility, dto.MeetingId, dto.AgendaItemId,
            dto.Options, dto.SpecificUserIds
        ));
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("active")]
    [Authorize] // or AllowAnonymous if Public should be visible to all
    public async Task<ActionResult<IReadOnlyList<VoteSummaryDto>>> Active()
        => Ok(await _mediator.Send(new GetActiveVotesQuery()));

    [HttpGet("recent")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<VoteSummaryDto>>> Recent()
        => Ok(await _mediator.Send(new GetRecentVotesQuery()));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<VoteDetailDto>> Get(Guid id)
        => Ok(await _mediator.Send(new GetVoteQuery(id)));

    public sealed record SubmitDto(VoteChoice? Choice, Guid? OptionId);

    [HttpPost("{id:guid}/ballots")]
    [Authorize]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitDto body)
    {
        await _mediator.Send(new SubmitBallotCommand(id, body.Choice, body.OptionId));
        return NoContent();
    }
}
