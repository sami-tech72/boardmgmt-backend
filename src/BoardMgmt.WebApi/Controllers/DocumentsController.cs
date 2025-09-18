using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Documents.Commands.UploadDocuments;
using BoardMgmt.Application.Documents.DTOs;
using BoardMgmt.Application.Documents.Queries.ListDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController(IMediator mediator, IFileStorage storage) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(
        [FromQuery] string? folderSlug,
        [FromQuery] string? type,
        [FromQuery] string? search,
        [FromQuery] string? datePreset)
        => Ok(await mediator.Send(new ListDocumentsQuery(folderSlug, type, search, datePreset)));

    [HttpPost("upload")]
    [Authorize] // dynamic permission is enforced inside the handler
    [RequestSizeLimit(52428800 * 5)]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> Upload(
        [FromForm] Guid? meetingId,
        [FromForm] string? folderSlug,
        [FromForm] string? description)
    {
        var files = Request.Form.Files;
        if (files.Count == 0) return BadRequest("No files sent.");

        var items = new List<UploadDocumentsCommand.UploadItem>();
        foreach (var f in files)
            items.Add(new UploadDocumentsCommand.UploadItem(f.OpenReadStream(), f.FileName, f.ContentType, f.Length));

        var result = await mediator.Send(new UploadDocumentsCommand(
            meetingId,
            string.IsNullOrWhiteSpace(folderSlug) ? "root" : folderSlug!,
            description,
            items));

        return Ok(result);
    }
}
