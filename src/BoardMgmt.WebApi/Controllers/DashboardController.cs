using BoardMgmt.Application.Dashboard.Queries;
using BoardMgmt.WebApi.Common.Http; // ✅ use your extension methods
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET /api/dashboard/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        // If you have auth, pull userId from claims:
        // var userId = User?.FindFirst("sub")?.Value is string s && Guid.TryParse(s, out var g) ? g : (Guid?)null;
        Guid? userId = null;

        var dto = await _mediator.Send(new GetDashboardStatsQuery(userId), ct);
        return this.OkApi(dto);
    }

    // GET /api/dashboard/meetings?take=3
    [HttpGet("meetings")]
    public async Task<IActionResult> GetMeetings([FromQuery] int take = 3, CancellationToken ct = default)
    {
        // Clamp to a sensible range to avoid abuse
        take = Math.Clamp(take, 1, 20);

        var dto = await _mediator.Send(new GetRecentMeetingsQuery(take), ct);
        return this.OkApi(dto);
    }

    // GET /api/dashboard/documents?take=3
    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments([FromQuery] int take = 3, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 20);

        var dto = await _mediator.Send(new GetRecentDocumentsQuery(take), ct);
        return this.OkApi(dto);
    }

    // GET /api/dashboard/activity?take=10
    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] int take = 10, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);

        var dto = await _mediator.Send(new GetRecentActivityQuery(take), ct);
        return this.OkApi(dto);
    }

    // GET /api/dashboard/stats/detail?kind=meetings|documents|votes|messages&page=1&pageSize=10
    [HttpGet("stats/detail")]
    public async Task<IActionResult> GetStatsDetail([FromQuery] string kind, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        // If you have auth, scope messages by user:
        // var userId = User?.FindFirst("sub")?.Value is string s && Guid.TryParse(s, out var g) ? g : (Guid?)null;
        Guid? userId = null;

        var dto = await _mediator.Send(new GetStatsDetailQuery(kind, page, pageSize, userId), ct);
        return this.OkApi(dto);
    }

    // WebApi/Controllers/DashboardController.cs (add inside the class)
    [HttpGet("users/active-count")]
    public async Task<IActionResult> GetActiveUserCount(CancellationToken ct)
    {
        var total = await _mediator.Send(new GetActiveUserCountQuery(), ct);
        return this.OkApi(total);
    }


}
