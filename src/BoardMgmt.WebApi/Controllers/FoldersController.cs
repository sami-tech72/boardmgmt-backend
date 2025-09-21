using BoardMgmt.Application.Folders.Commands.CreateFolder;
using BoardMgmt.Application.Folders.DTOs;
using BoardMgmt.Application.Folders.Queries.GetFolders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;




namespace BoardMgmt.WebApi.Controllers;


[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly IMediator _mediator;
    public FoldersController(IMediator mediator) => _mediator = mediator;


    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<FolderDto>>> Get()
    => Ok(await _mediator.Send(new GetFoldersQuery()));


    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FolderDto>> Create([FromBody] CreateFolderCommand cmd)
    {
        try { return Ok(await _mediator.Send(cmd)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }
}