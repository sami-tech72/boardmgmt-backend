using BoardMgmt.Application.Votes.Commands;
using BoardMgmt.Application.Votes.DTOs;
using BoardMgmt.Application.Votes.Queries;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using BoardMgmt.WebApi.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VotesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserManager<AppUser> _userManager;

    public VotesController(IMediator mediator, UserManager<AppUser> userManager)
    {
        _mediator = mediator;
        _userManager = userManager;
    }

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
        List<string>? Options,
        List<string>? SpecificUserIds
    );

    [HttpPost]
    [Authorize(Policy = PolicyNames.Votes.Create)]
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
    [AllowAnonymous] // Public polls visible without login
    public async Task<ActionResult<IReadOnlyList<VoteSummaryDto>>> Active()
        => Ok(await _mediator.Send(new GetActiveVotesQuery()));

    [HttpGet("recent")]
    [Authorize(Policy = PolicyNames.Votes.View)]
    public async Task<ActionResult<IReadOnlyList<VoteSummaryDto>>> Recent()
        => Ok(await _mediator.Send(new GetRecentVotesQuery()));

    [HttpGet("{id:guid}")]
    [AllowAnonymous] // Allow public to view a public poll detail
    public async Task<ActionResult<VoteDetailDto>> Get(Guid id)
    {
        var dto = await _mediator.Send(new GetVoteQuery(id));
        if (dto is null) return NotFound();

        // Enrich individual votes with display names if non-anonymous
        if (!dto.Anonymous && dto.IndividualVotes is { Count: > 0 })
        {
            var ids = dto.IndividualVotes.Select(iv => iv.UserId).Distinct().ToArray();

            // Works with EF Core stores for Identity. If not EF, replace this with _userManager.FindByIdAsync in a loop.
            var map = await _userManager.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName, u.Email })
                .ToDictionaryAsync(x => x.Id);

            dto = dto with
            {
                IndividualVotes = dto.IndividualVotes
                    .Select(iv =>
                    {
                        var has = map.TryGetValue(iv.UserId, out var u);
                        var display = has ? (u!.UserName ?? u.Email ?? iv.UserId) : iv.UserId;
                        return iv with { DisplayName = display };
                    })
                    .ToList()
            };
        }

        return Ok(dto);
    }

    public sealed record SubmitDto(VoteChoice? Choice, Guid? OptionId);

    // BoardMgmt.WebApi/Controllers/VotesController.cs
    [HttpPost("{id:guid}/ballots")]
    [Authorize(Policy = PolicyNames.Votes.View)]
    public async Task<ActionResult<VoteSummaryDto>> Submit(Guid id, [FromBody] SubmitDto body)
    {
        var summary = await _mediator.Send(new SubmitBallotCommand(id, body.Choice, body.OptionId));
        return Ok(summary);
    }
}
