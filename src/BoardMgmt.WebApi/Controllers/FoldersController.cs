using BoardMgmt.Application.Folders.Commands.CreateFolder;
using BoardMgmt.Application.Folders.DTOs;
using BoardMgmt.Application.Folders.Queries.GetFolders;
using BoardMgmt.WebApi.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Folders.View")] // class-level; GET is explicitly AllowAnonymous below
public class FoldersController : ControllerBase
{
    private readonly IMediator _mediator;
    public FoldersController(IMediator mediator) => _mediator = mediator;

    // -------------------- READ --------------------

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var folders = await _mediator.Send(new GetFoldersQuery(), ct);
        return this.OkApi(folders);
    }

    // -------------------- CREATE --------------------

    [HttpPost]
    [Authorize(Policy = "Folders.Create")]
    public async Task<IActionResult> Create([FromBody] CreateFolderCommand cmd, CancellationToken ct)
    {
        try
        {
            var dto = await _mediator.Send(cmd, ct);
            // 201 + uniform body; no per-id route, so point Location at the list
            return this.CreatedApi(nameof(Get), routeValues: null, dto, "Folder created.");
        }
        catch (ArgumentException ex)
        {
            // 400 uniform body
            return this.BadRequestApi("folders.invalid_argument", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // 409 uniform body (no ConflictApi helper provided, so construct ObjectResult)
            var payload = ApiErrorResponse.From(
                StatusCodes.Status409Conflict,
                code: "folders.conflict",
                message: ex.Message,
                details: null,
                traceId: HttpContext.TraceIdentifier);

            return new ObjectResult(payload) { StatusCode = StatusCodes.Status409Conflict };
        }
    }
}
