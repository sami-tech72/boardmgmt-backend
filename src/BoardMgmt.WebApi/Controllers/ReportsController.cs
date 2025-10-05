using BoardMgmt.Application.Reports.Commands;
using BoardMgmt.Application.Reports.Queries;
using BoardMgmt.WebApi.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]

public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReportsController(IMediator mediator) => _mediator = mediator;

    // GET: /api/reports/dashboard?months=6
    [HttpGet("dashboard")]
    [Authorize]
    public async Task<IActionResult> Dashboard([FromQuery] int months = 6)
        => this.OkApi(await _mediator.Send(new GetReportsDashboardQuery(months)));

    // GET: /api/reports/recent?take=10
    [HttpGet("recent")]
    [Authorize]
    public async Task<IActionResult> Recent([FromQuery] int take = 10)
        => this.OkApi(await _mediator.Send(new GetRecentReportsQuery(take)));

    // POST: /api/reports/generate
    [HttpPost("generate")]
    [Authorize]
    public async Task<IActionResult> Generate([FromBody] GenerateReportCommand command)
    {
        var id = await _mediator.Send(command);
        return this.OkApi(new { id });
    }
}
